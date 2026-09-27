import { backendUrl } from "./backend";
export type User = {
  hasPassword?: boolean;
  id: string;
  displayName: string;
  email: string;
  role: "Admin" | "Agent" | "Requester";
  organization: string;
  organizationId: string;
  teamId: string | null;
};
export type Status =
  | "New"
  | "InProgress"
  | "WaitingRequester"
  | "WaitingThirdParty"
  | "Resolved"
  | "Closed"
  | "Cancelled";
export type Priority = "Low" | "Normal" | "High" | "Urgent";
export type Ticket = {
  id: string;
  number: string;
  subject: string;
  category: string;
  moduleId?: string;
  module?: string;
  organizationId?: string;
  status: Status;
  priority: Priority;
  createdAt: string;
  updatedAt: string;
  dueAt: string;
  requester: string;
  organization: string;
  assignee: string | null;
  hasCustomerReply: boolean;
};
export type TicketDetail = Omit<Ticket, "requester"> & {
  version?: string;
  requester: { displayName: string; email: string };
  attachments: { id: string; fileName: string; length: number }[];
  assigneeId: string | null;
  nextStatuses: Status[];
  messages: {
    id: number;
    body: string;
    visibility: "Public" | "Internal";
    source: string;
    createdAt: string;
    author: string;
    own: boolean;
  }[];
  events:
    | {
        id: number;
        kind: string;
        detail: string;
        createdAt: string;
        actor: string;
      }[]
    | null;
};
export type SupportModule = { id: string; name: string; category: string };
export type Directory = {
  modules: SupportModule[];
  categories: string[];
  agents:
    | {
        id: string;
        displayName: string;
        teamId: string | null;
        role: string;
        moduleIds: string[];
      }[]
    | null;
  organizations: { id: string; name: string }[] | null;
  teams: { id: string; name: string }[] | null;
  users:
    | {
        id: string;
        displayName: string;
        email: string;
        role: string;
        invitationPending?: boolean;
        moduleIds: string[];
        organizationIds: string[];
        organizationId: string;
      }[]
    | null;
};
export const statuses: Record<Status, string> = {
  New: "Nuevo",
  InProgress: "En curso",
  WaitingRequester: "Esperando respuesta",
  WaitingThirdParty: "Esperando tercero",
  Resolved: "Resuelto",
  Closed: "Cerrado",
  Cancelled: "Cancelado",
};
export const priorities: Record<Priority, string> = {
  Low: "Baja",
  Normal: "Normal",
  High: "Alta",
  Urgent: "Urgente",
};
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}
let csrf = "";
export async function refreshCsrf() {
  const response = await fetch(backendUrl("/api/v1/auth/csrf"), {
    credentials: "include",
  });
  if (!response.ok) throw new Error("No se pudo conectar con Sidecil.");
  csrf = (await response.json()).token;
}
export async function api<T>(
  path: string,
  options: RequestInit = {},
): Promise<{ data: T; etag: string }> {
  const method = options.method || "GET";
  if (method !== "GET" && !csrf) await refreshCsrf();
  const response = await fetch(backendUrl("/api/v1" + path), {
    ...options,
    credentials: "include",
    headers: {
      ...(options.body instanceof FormData
        ? {}
        : { "Content-Type": "application/json" }),
      ...(method !== "GET" ? { "X-CSRF-TOKEN": csrf } : {}),
      ...options.headers,
    },
  });
  if (!response.ok) {
    if (
      response.status === 401 &&
      path !== "/auth/login" &&
      path !== "/auth/me"
    ) {
      csrf = "";
      window.dispatchEvent(new Event("sidecil:session-expired"));
    }
    const problem = await response.json().catch(() => ({}));
    throw new ApiError(
      response.status,
      problem.detail ||
        (response.status === 401
          ? "Tu sesión ha terminado. Ingresa nuevamente."
          : "No fue posible completar la solicitud."),
    );
  }
  return {
    data: response.status === 204 ? (undefined as T) : await response.json(),
    etag: response.headers.get("ETag") || "",
  };
}
export const date = (value: string) =>
  new Intl.DateTimeFormat("es-CO", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value.endsWith("Z") ? value : value + "Z"));
export const initials = (name: string) =>
  name
    .split(" ")
    .slice(0, 2)
    .map((x) => x[0])
    .join("")
    .toUpperCase();
export const closed = (status: Status) =>
  ["Resolved", "Closed", "Cancelled"].includes(status);
