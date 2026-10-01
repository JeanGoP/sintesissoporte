import { ClassifyTicket } from "./classify-ticket";
import { TicketAttachments } from "./attachment-preview";
import { ReplyTemplatePicker } from "./reply-templates";
import { useState, useEffect, useRef } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Snackbar,
} from "@mui/material";
import {
  ArrowLeft,
  RefreshCw,
  MessageSquare,
  LockKeyhole,
  Send,
  Users,
  CheckCircle2,
  Clock3,
  ShieldCheck,
} from "lucide-react";
import {
  api,
  ApiError,
  date,
  initials,
  statuses,
  type User,
  type TicketDetail,
  type Directory,
  type Status,
} from "./api";
import { ErrorBox, Loading, StatusBadge, PriorityBadge } from "./shared";
export function DetailPage({ user }: { user: User }) {
  const { id } = useParams(),
    navigate = useNavigate(),
    client = useQueryClient(),
    staff = user.role !== "Requester";
  const [draft, setDraft] = useState(""),
    [visibility, setVisibility] = useState<"Public" | "Internal">("Public"),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false),
    [next, setNext] = useState<Status | "">(""),
    [reason, setReason] = useState(""),
    [notice, setNotice] = useState("");
  const seenEvent = useRef("");
  const detail = useQuery({
    queryKey: ["ticket", id],
    queryFn: () => api<TicketDetail>("/tickets/" + id, { cache: "no-store" }),
    refetchInterval: busy ? false : 5000,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: !busy,
    refetchOnReconnect: !busy,
  });
  const directory = useQuery({
    queryKey: ["directory"],
    queryFn: async () => (await api<Directory>("/directory")).data,
  });
  useEffect(() => {
    setDraft("");
    setError(undefined);
    setVisibility("Public");
  }, [id]);
  useEffect(() => {
    if (!staff || !id || !detail.data?.data) return;
    const latest = detail.data.data.events?.[0]?.id ?? 0;
    const key = id + ":" + latest;
    if (seenEvent.current === key) return;
    seenEvent.current = key;
    void api("/tickets/" + id + "/seen", { method: "POST" })
      .then(() => client.invalidateQueries({ queryKey: ["tickets"] }))
      .catch(() => { seenEvent.current = ""; });
  }, [staff, id, detail.data, client]);
  async function mutate(path: string, body: unknown, method = "POST") {
    setBusy(true);
    setError(undefined);
    try {
      const version = detail.data?.data.version || detail.data?.etag;
      if (!version)
        throw new Error(
          "No se pudo obtener la versión del ticket. Pulsa Actualizar antes de guardar.",
        );
      await api("/tickets/" + id + path, {
        method,
        headers: { "If-Match": version },
        body: JSON.stringify(body),
      });
      await detail.refetch();
      await client.invalidateQueries({ queryKey: ["tickets"] });
      return true;
    } catch (e) {
      setError(e);
      return false;
    } finally {
      setBusy(false);
    }
  }
  const t = detail.data?.data;
  if (detail.isPending) return <Loading />;
  if (detail.error instanceof ApiError && detail.error.status === 404)
    return (
      <>
        <Button startIcon={<ArrowLeft size={16} />} onClick={() => navigate("/")}>Volver a la bandeja</Button>
        <p>Este ticket ya no está disponible para tu cuenta.</p>
      </>
    );
  if (!t)
    return (
      <>
        <Button
          startIcon={<ArrowLeft size={16} />}
          onClick={() => navigate("/")}
        >
          Volver a la bandeja
        </Button>
        <ErrorBox error={detail.error} />
      </>
    );
  return (
    <>
      <div className="detail-top">
        <Button
          startIcon={<ArrowLeft size={16} />}
          onClick={() => navigate("/")}
        >
          Bandeja de tickets
        </Button>
        <button
          className="text-button"
          disabled={busy}
          onClick={async () => {
            const refreshed = await detail.refetch();
            if (refreshed.error) {
              setError(refreshed.error);
              return;
            }
            setError(undefined);
            setNotice("Ticket actualizado. Tu borrador se conservó.");
          }}
        >
          <RefreshCw size={16} />
          Actualizar
        </button>
      </div>
      <div className="detail-heading">
        <span className="eyebrow">
          {t.number} <span className="dot-separator">/</span> {t.category} ·{" "}
          {t.module || "Sin módulo: pendiente de clasificación"}
        </span>
        <h1>{t.subject}</h1>
        <div className="detail-badges">
          <StatusBadge status={t.status} />
          <PriorityBadge priority={t.priority} />
          <span>Creado el {date(t.createdAt)}</span>
        </div>
      </div>
      <ErrorBox error={error || detail.error} />
      <div className="detail-grid">
        <section className="conversation">
          <div className="section-heading">
            <MessageSquare size={19} />
            <h2>Conversación</h2>
            <span className="count">{t.messages.length}</span>
          </div>
          {t.attachments?.length > 0 && <TicketAttachments key={t.id} ticketId={t.id} files={t.attachments} />}
          <div className="messages">
            {t.messages.map((m) => (
              <article
                key={m.id}
                className={
                  "message " +
                  (m.visibility === "Internal" ? "private-message" : "")
                }
              >
                <span className="avatar">{initials(m.author)}</span>
                <div className="message-content">
                  <div className="message-head">
                    <strong>{m.author}</strong>
                    {m.source === "Email" && (
                      <span className="badge">Por correo</span>
                    )}
                    {m.visibility === "Internal" && (
                      <span className="private-label">
                        <LockKeyhole size={12} />
                        Nota interna
                      </span>
                    )}
                    <time>{date(m.createdAt)}</time>
                  </div>
                  <p>{m.body}</p>
                </div>
              </article>
            ))}
          </div>
          {!["Closed", "Cancelled"].includes(t.status) ? (
            <form
              className={
                "composer " +
                (visibility === "Internal" ? "private-composer" : "")
              }
              onSubmit={async (e) => {
                e.preventDefault();
                if (await mutate("/messages", { body: draft, visibility })) {
                  setDraft("");
                  setNotice(
                    visibility === "Internal"
                      ? "Nota interna guardada."
                      : "Respuesta enviada.",
                  );
                }
              }}
            >
              <div className="composer-tabs">
                <button
                  type="button"
                  disabled={busy}
                  className={visibility === "Public" ? "active" : ""}
                  onClick={() => setVisibility("Public")}
                >
                  <Send size={15} />
                  Respuesta pública
                </button>
                {staff && (
                  <button
                    type="button"
                    disabled={busy}
                    className={visibility === "Internal" ? "active" : ""}
                    onClick={() => setVisibility("Internal")}
                  >
                    <LockKeyhole size={15} />
                    Nota interna
                  </button>
                )}
              </div>
              {staff && visibility === "Public" && (
                <ReplyTemplatePicker name={t.requester.displayName} number={t.number} subject={t.subject}
                  insert={(body) => {
                    const next = [draft.trimEnd(), body].filter(Boolean).join("\n\n");
                    if (next.length > 12000) { setError(new Error("La respuesta supera los 12.000 caracteres.")); return; }
                    setDraft(next);
                    document.getElementById("reply")?.focus();
                  }} />
              )}
              <label className="sr-only" htmlFor="reply">
                Mensaje
              </label>
              <textarea
                id="reply"
                required
                maxLength={12000}
                disabled={busy}
                placeholder={
                  visibility === "Internal"
                    ? "Escribe una nota para el equipo autorizado…"
                    : "Escribe tu respuesta…"
                }
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
              />
              <div className="composer-footer">
                <span>
                  {visibility === "Internal" ? (
                    <LockKeyhole size={14} />
                  ) : (
                    <Users size={14} />
                  )}{" "}
                  {visibility === "Internal"
                    ? "Solo visible para agentes autorizados"
                    : "Visible para el solicitante y el equipo"}
                </span>
                <Button
                  variant="contained"
                  type="submit"
                  disabled={busy || !draft.trim()}
                  endIcon={<Send size={15} />}
                >
                  {visibility === "Internal"
                    ? "Guardar nota"
                    : "Enviar respuesta"}
                </Button>
              </div>
            </form>
          ) : (
            <div className="closed-note">
              <CheckCircle2 size={20} />
              La conversación está cerrada.
            </div>
          )}
          {staff && (
            <details className="audit">
              <summary>
                Historial de actividad <span>{t.events?.length || 0}</span>
              </summary>
              {t.events?.map((e) => (
                <div key={e.id}>
                  <span className="timeline-dot" />
                  <div>
                    <strong>{e.actor}</strong>
                    <p>{e.detail}</p>
                    <small>{date(e.createdAt)}</small>
                  </div>
                </div>
              ))}
            </details>
          )}
        </section>
        <aside className="ticket-context">
          <section className="context-card">
            <span className="eyebrow">SOLICITANTE</span>
            <div className="requester-card">
              <span className="avatar large">
                {initials(t.requester.displayName)}
              </span>
              <strong>{t.requester.displayName}</strong>
              <span>{t.requester.email}</span>
              <small>{t.organization}</small>
            </div>
          </section>
          <section className="context-card">
            <h3>Detalles de atención</h3>
            <p>
              {t.category} / {t.module || "Sin módulo"}
            </p>
            {user.role === "Admin" && directory.data && (
              <ClassifyTicket
                ticket={t}
                directory={directory.data}
                busy={busy}
                save={(body) => mutate("/classification", body, "PUT")}
              />
            )}
            <label className="field-label">Responsable</label>
            {staff ? (
              <select
                aria-label="Responsable"
                className="wide-select"
                value={t.assigneeId || ""}
                disabled={busy}
                onChange={(e) =>
                  mutate(
                    "/assignment",
                    { assigneeId: e.target.value || null },
                    "PUT",
                  )
                }
              >
                <option value="" disabled={user.role !== "Admin"}>
                  Sin asignar
                </option>
                {directory.data?.agents
                  ?.filter(
                    (a) =>
                      (a.role === "Admin" ||
                        a.moduleIds?.includes(t.moduleId || "")) &&
                      (user.role === "Admin" || a.id === user.id),
                  )
                  .map((a) => (
                    <option value={a.id} key={a.id}>
                      {a.displayName}
                    </option>
                  ))}
              </select>
            ) : (
              <p>Equipo de atención Sidecil</p>
            )}
            <label className="field-label">Objetivo de resolución</label>
            <div className="target-date">
              <Clock3 size={17} />
              {date(t.dueAt)}
            </div>
            <p className="small-note">
              Objetivo en horas corridas según prioridad. Aún no aplica
              calendario laboral.
            </p>
            {staff && t.nextStatuses.length > 0 && (
              <>
                <label className="field-label">Cambiar estado</label>
                <select
                  aria-label="Cambiar estado"
                  disabled={busy}
                  className="wide-select"
                  value=""
                  onChange={async (e) => {
                    const status = e.target.value as Status;
                    if (t.status === "New" && status === "InProgress") {
                      if (await mutate("/transitions", {
                        status,
                        reason: "Inicio de la atención del ticket.",
                      })) setNotice("Ticket en curso.");
                      return;
                    }
                    setNext(status);
                    setReason("");
                    setError(undefined);
                  }}
                >
                  <option value="">Seleccionar cambio…</option>
                  {t.nextStatuses.map((s) => (
                    <option key={s} value={s}>
                      {statuses[s]}
                    </option>
                  ))}
                </select>
              </>
            )}
          </section>
          <div className="context-tip">
            <ShieldCheck size={20} />
            <p>
              La información de esta solicitud está disponible únicamente para
              las personas autorizadas.
            </p>
          </div>
        </aside>
      </div>
      <Dialog
        open={!!next}
        onClose={() => !busy && setNext("")}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>Cambiar a {next && statuses[next]}</DialogTitle>
        <DialogContent>
          <p className="form-note">
            Registra el motivo o la solución para conservar la trazabilidad
            interna. La respuesta al solicitante se envía por separado en la
            conversación.
          </p>
          <TextField
            fullWidth
            autoFocus
            multiline
            minRows={3}
            label="Motivo o solución (interno)"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            inputProps={{ maxLength: 2000 }}
          />
          <ErrorBox error={error || detail.error} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setNext("")} disabled={busy}>
            Cancelar
          </Button>
          <Button
            variant="contained"
            disabled={busy || reason.trim().length < 3}
            onClick={async () => {
              if (await mutate("/transitions", { status: next, reason })) {
                setNext("");
                setNotice("Estado actualizado.");
              }
            }}
          >
            Guardar cambio
          </Button>
        </DialogActions>
      </Dialog>
      <Snackbar
        open={!!notice}
        message={notice}
        autoHideDuration={3500}
        onClose={() => setNotice("")}
      />
    </>
  );
}
