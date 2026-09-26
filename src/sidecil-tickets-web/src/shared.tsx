import { Alert, CircularProgress } from "@mui/material";
import { statuses, priorities, type Status, type Priority } from "./api";
export const StatusBadge = ({ status }: { status: Status }) => (
  <span className={"badge status-" + status}>
    <i />
    {statuses[status]}
  </span>
);
export const PriorityBadge = ({ priority }: { priority: Priority }) => (
  <span className={"priority priority-" + priority}>
    <span>
      {priority === "Urgent"
        ? "↑↑"
        : priority === "High"
          ? "↑"
          : priority === "Low"
            ? "↓"
            : "−"}
    </span>
    {priorities[priority]}
  </span>
);
export function ErrorBox({ error }: { error: unknown }) {
  return error ? (
    <Alert severity="error">
      {error instanceof Error ? error.message : "Ocurrió un error inesperado."}
    </Alert>
  ) : null;
}
export function Loading() {
  return (
    <div className="loading">
      <CircularProgress size={26} />
      <span>Cargando tu espacio…</span>
    </div>
  );
}
