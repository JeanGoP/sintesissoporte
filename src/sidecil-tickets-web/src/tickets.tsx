import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  MenuItem,
} from "@mui/material";
import {
  Plus,
  Inbox,
  Users,
  Clock3,
  TicketCheck,
  RefreshCw,
  Search,
  ArrowUpRight,
  ChevronLeft,
  ChevronRight,
  ShieldCheck,
  ArrowRight,
} from "lucide-react";
import {
  api,
  initials,
  statuses,
  priorities,
  type User,
  type Ticket,
  type Priority,
  type Directory,
} from "./api";
import { ErrorBox, Loading, StatusBadge, PriorityBadge } from "./shared";
type Summary = {
  active: number;
  unassigned: number;
  overdue: number;
  resolved: number;
};
type TicketList = {
  items: Ticket[];
  total: number;
  page: number;
  pageSize: number;
  summary: Summary;
};
function useTickets(search = "") {
  return useQuery({
    queryKey: ["tickets", search],
    queryFn: async () =>
      (await api<TicketList>("/tickets" + search, { cache: "no-store" })).data,
    refetchInterval: 5000,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: true,
    refetchOnReconnect: true,
  });
}
function Stats({ summary }: { summary: Summary }) {
  const cards = [
    {
      label: "Tickets activos",
      value: summary.active,
      icon: Inbox,
      note: "Por atender y en seguimiento",
      style: "green",
    },
    {
      label: "Sin asignar",
      value: summary.unassigned,
      icon: Users,
      note: "Esperando un responsable",
      style: "blue",
    },
    {
      label: "Objetivo vencido",
      value: summary.overdue,
      icon: Clock3,
      note: "Necesitan tu atención",
      style: "amber",
    },
    {
      label: "Resueltos y cerrados",
      value: summary.resolved,
      icon: TicketCheck,
      note: "Solicitudes completadas",
      style: "sage",
    },
  ];
  return (
    <div className="stats">
      {cards.map((c) => (
        <article className={"stat-card " + c.style} key={c.label}>
          <div className="stat-heading">
            <span>{c.label}</span>
            <span className="stat-icon">
              <c.icon size={18} />
            </span>
          </div>
          <strong>{String(c.value).padStart(2, "0")}</strong>
          <small>{c.note}</small>
        </article>
      ))}
    </div>
  );
}
export function InboxPage({
  user,
  onCreate,
}: {
  user: User;
  onCreate: () => void;
}) {
  const navigate = useNavigate(),
    [search, setSearch] = useState(""),
    [term, setTerm] = useState(""),
    [status, setStatus] = useState(""),
    [priority, setPriority] = useState(""),
    [view, setView] = useState(""),
    [page, setPage] = useState(1),
    staff = user.role !== "Requester";
  useEffect(() => {
    const timer = setTimeout(() => {
      setTerm(search);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);
  const query = useTickets(
    "?" +
      new URLSearchParams({
        search: term,
        status,
        priority,
        view,
        page: String(page),
      }),
  );
  const filter = (setter: (v: string) => void, value: string) => {
    setter(value);
    setPage(1);
  };
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">
            {staff ? "TU CENTRO DE OPERACIONES" : "ESTAMOS PARA AYUDARTE"}
          </span>
          <h1>
            {staff
              ? "Cada ticket cuenta."
              : "Tus solicitudes, en un solo lugar."}
          </h1>
          <p>
            {staff
              ? "Organiza, conecta y resuelve. Todo tu soporte, en un solo lugar."
              : "Crea una solicitud y sigue la conversación con nuestro equipo."}
          </p>
        </div>
        <Button
          variant="contained"
          size="large"
          startIcon={<Plus size={19} />}
          onClick={onCreate}
        >
          Nuevo ticket
        </Button>
      </div>
      {query.data && <Stats summary={query.data.summary} />}
      <section className="inbox-panel">
        <div className="panel-title">
          <div>
            <h2>
              {staff ? "Bandeja de tickets" : "Mis tickets"}{" "}
              <span className="count">{query.data?.total ?? "—"}</span>
            </h2>
            <p>Las conversaciones que hacen avanzar tu día.</p>
          </div>
          <button
            className="icon-button"
            aria-label="Actualizar bandeja"
            onClick={() => query.refetch()}
          >
            <RefreshCw size={18} />
          </button>
        </div>
        <div className="tabs">
          {staff && (
            <button
              className={view === "replies" ? "active" : ""}
              onClick={() => filter(setView, "replies")}
            >
              Respuestas de clientes
            </button>
          )}
          <button
            className={!view ? "active" : ""}
            onClick={() => filter(setView, "")}
          >
            Todos los tickets
          </button>
          {staff && (
            <>
              <button
                className={view === "mine" ? "active" : ""}
                onClick={() => filter(setView, "mine")}
              >
                Asignados a mí
              </button>
              <button
                className={view === "unassigned" ? "active" : ""}
                onClick={() => filter(setView, "unassigned")}
              >
                Sin asignar
              </button>
            </>
          )}
          <button
            className={view === "overdue" ? "active" : ""}
            onClick={() => filter(setView, "overdue")}
          >
            Requieren atención
          </button>
        </div>
        <div className="filters">
          <label className="search-field">
            <Search size={18} />
            <input
              placeholder="Buscar por asunto o número de ticket…"
              aria-label="Buscar tickets"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <select
            aria-label="Filtrar por estado"
            value={status}
            onChange={(e) => filter(setStatus, e.target.value)}
          >
            <option value="">Todos los estados</option>
            {Object.entries(statuses).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
          <select
            aria-label="Filtrar por prioridad"
            value={priority}
            onChange={(e) => filter(setPriority, e.target.value)}
          >
            <option value="">Todas las prioridades</option>
            {Object.entries(priorities).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
        </div>
        <ErrorBox error={query.error} />
        {query.isPending ? (
          <Loading />
        ) : query.data?.items.length ? (
          <div className="table-scroll">
            <table className="tickets-table ticket-list">
              <thead>
                <tr>
                  <th>Ticket / Asunto</th>
                  <th>Solicitante</th>
                  <th>Estado</th>
                  <th>Prioridad</th>
                  <th>Responsable</th>
                  <th>
                    <span className="sr-only">Abrir</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {query.data.items.map((t) => (
                  <tr key={t.id}>
                    <td>
                      <button
                        className="ticket-link"
                        onClick={() => navigate("/tickets/" + t.id)}
                      >
                        {staff && t.hasCustomerReply && (
                          <span className="customer-reply">
                            Respuesta del cliente
                          </span>
                        )}
                        <span className="ticket-meta">
                          <span>{t.number}</span>
                          <span>·</span>
                          <span>{t.category}</span>
                        </span>
                        <strong>{t.subject}</strong>
                        <span className="ticket-date">
                          {new Date(
                            t.createdAt.endsWith("Z")
                              ? t.createdAt
                              : t.createdAt + "Z",
                          ).toLocaleDateString("es-CO", {
                            day: "numeric",
                            month: "short",
                          })}
                        </span>
                      </button>
                    </td>
                    <td>
                      <div className="person">
                        <span className="avatar muted">
                          {initials(t.requester)}
                        </span>
                        <div>
                          <strong>{t.requester}</strong>
                          <small>{t.organization}</small>
                        </div>
                      </div>
                    </td>
                    <td>
                      <StatusBadge status={t.status} />
                    </td>
                    <td>
                      <PriorityBadge priority={t.priority} />
                    </td>
                    <td>
                      {t.assignee ? (
                        <div className="assignee">
                          <span className="avatar tiny">
                            {initials(t.assignee)}
                          </span>
                          {t.assignee.split(" ")[0]}
                        </div>
                      ) : (
                        <span className="unassigned">Sin asignar</span>
                      )}
                    </td>
                    <td>
                      <button
                        className="icon-button"
                        aria-label={"Abrir " + t.number}
                        onClick={() => navigate("/tickets/" + t.id)}
                      >
                        <ArrowUpRight size={17} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="empty">
            <Inbox size={35} />
            <h3>No hay tickets en esta vista</h3>
            <p>Prueba otros filtros o crea una solicitud.</p>
            <Button onClick={onCreate}>Crear ticket</Button>
          </div>
        )}
        <div className="pagination">
          <span>
            {query.data?.total
              ? Math.min((page - 1) * 25 + 1, query.data.total)
              : 0}
            –{Math.min(page * 25, query.data?.total || 0)} de{" "}
            {query.data?.total || 0} tickets
          </span>
          <div>
            <button
              className="icon-button"
              aria-label="Página anterior"
              disabled={page <= 1}
              onClick={() => setPage(page - 1)}
            >
              <ChevronLeft size={18} />
            </button>
            <span>Página {page}</span>
            <button
              className="icon-button"
              aria-label="Página siguiente"
              disabled={page * 25 >= (query.data?.total || 0)}
              onClick={() => setPage(page + 1)}
            >
              <ChevronRight size={18} />
            </button>
          </div>
        </div>
      </section>
      <div className="info-strip">
        <ShieldCheck size={17} />
        <span>
          {staff
            ? "Las notas internas solo son visibles para agentes autorizados."
            : "Solo tú y el equipo autorizado pueden consultar tus solicitudes."}
        </span>
      </div>
    </>
  );
}
export function CreateDialog({
  open,
  onClose,
}: {
  open: boolean;
  onClose: () => void;
}) {
  const navigate = useNavigate(),
    client = useQueryClient(),
    [subject, setSubject] = useState(""),
    [body, setBody] = useState(""),
    [category, setCategory] = useState("General"),
    [priority, setPriority] = useState<Priority>("Normal"),
    [busy, setBusy] = useState(false),
    [error, setError] = useState<unknown>();
  const directory = useQuery({
    queryKey: ["directory"],
    queryFn: async () => (await api<Directory>("/directory")).data,
  });
  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      const r = await api<{ id: string }>("/tickets", {
        method: "POST",
        body: JSON.stringify({ subject, body, category, priority }),
      });
      await client.invalidateQueries({ queryKey: ["tickets"] });
      setSubject("");
      setBody("");
      onClose();
      navigate("/tickets/" + r.data.id);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Dialog
      open={open}
      onClose={() => !busy && onClose()}
      fullWidth
      maxWidth="sm"
    >
      <form onSubmit={submit}>
        <DialogTitle>
          <span className="eyebrow">UNA SOLICITUD, UNA CONVERSACIÓN</span>
          <br />
          Crear nuevo ticket
        </DialogTitle>
        <DialogContent>
          <div className="form-fields">
            <p className="form-note">
              Cuéntanos qué necesitas. Entre más contexto compartas, mejor
              podremos ayudarte.
            </p>
            <TextField
              autoFocus
              required
              label="Asunto"
              inputProps={{ minLength: 5, maxLength: 180 }}
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
            />
            <div className="form-row">
              <TextField
                select
                fullWidth
                label="Categoría"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
              >
                {(directory.data?.categories || ["General"]).map((x) => (
                  <MenuItem value={x} key={x}>
                    {x}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                fullWidth
                label="Prioridad"
                value={priority}
                onChange={(e) => setPriority(e.target.value as Priority)}
              >
                {Object.entries(priorities).map(([k, v]) => (
                  <MenuItem key={k} value={k}>
                    {v}
                  </MenuItem>
                ))}
              </TextField>
            </div>
            <TextField
              required
              multiline
              minRows={5}
              label="Describe tu solicitud"
              inputProps={{ minLength: 10, maxLength: 12000 }}
              value={body}
              onChange={(e) => setBody(e.target.value)}
            />
            <ErrorBox error={error} />
          </div>
        </DialogContent>
        <DialogActions>
          <Button disabled={busy} onClick={onClose}>
            Cancelar
          </Button>
          <Button
            variant="contained"
            type="submit"
            disabled={busy}
            endIcon={<ArrowRight size={16} />}
          >
            {busy ? "Creando…" : "Crear ticket"}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
export function ReportsPage({ user }: { user: User }) {
  const query = useTickets(),
    s = query.data?.summary;
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">DATOS PARA ACTUAR</span>
          <h1>
            {user.role === "Requester"
              ? "Así van tus solicitudes."
              : "Una mirada a la operación."}
          </h1>
          <p>Estado actual de los tickets que tienes permiso de consultar.</p>
        </div>
        <Button
          onClick={() => query.refetch()}
          startIcon={<RefreshCw size={17} />}
        >
          Actualizar
        </Button>
      </div>
      <ErrorBox error={query.error} />
      {query.isPending ? (
        <Loading />
      ) : (
        s && (
          <>
            <Stats summary={s} />
            <section className="report-card">
              <h2>Distribución operativa</h2>
              <p>
                Los tickets sin asignar o vencidos también forman parte de los
                activos.
              </p>
              {[
                { label: "Activos", value: s.active, color: "#3657db" },
                {
                  label: "Resueltos y cerrados",
                  value: s.resolved,
                  color: "#00b9ed",
                },
                { label: "Sin asignar", value: s.unassigned, color: "#91b7f5" },
                {
                  label: "Objetivo vencido",
                  value: s.overdue,
                  color: "#d39f53",
                },
              ].map((x) => (
                <div className="report-row" key={x.label}>
                  <span>{x.label}</span>
                  <div>
                    <i
                      style={{
                        width:
                          (x.value / Math.max(s.active + s.resolved, 1)) * 100 +
                          "%",
                        background: x.color,
                      }}
                    />
                  </div>
                  <strong>{x.value}</strong>
                </div>
              ))}
            </section>
            <div className="info-strip">
              <ShieldCheck size={17} />
              <span>
                Datos actuales. SLA contractual y satisfacción se incorporarán
                en una siguiente entrega.
              </span>
            </div>
          </>
        )
      )}
    </>
  );
}
