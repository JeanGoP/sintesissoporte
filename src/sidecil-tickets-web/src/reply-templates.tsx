import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from "@mui/material";
import { Plus, MessageSquareText } from "lucide-react";
import { api } from "./api";
import { ErrorBox, Loading } from "./shared";

type ReplyTemplate = { id: string; title: string; body: string; enabled: boolean; updatedAt: string };
function useReplyTemplates(admin = false) {
  return useQuery({
    queryKey: ["reply-templates", admin ? "admin" : "active"],
    queryFn: async () => (await api<ReplyTemplate[]>(admin ? "/admin/reply-templates" : "/reply-templates", { cache: "no-store" })).data,
    refetchInterval: admin ? false : 60000,
  });
}

export function ReplyTemplatePicker({ name, number, subject, insert }: {
  name: string; number: string; subject: string; insert: (body: string) => void;
}) {
  const templates = useReplyTemplates();
  const [selected, setSelected] = useState("");
  const current = templates.data?.find((template) => template.id === selected);
  return (
    <div className="reply-template-picker">
      <MessageSquareText size={17} />
      <select aria-label="Respuesta rápida" value={selected} onChange={(event) => setSelected(event.target.value)}>
        <option value="">Elegir respuesta rápida…</option>
        {templates.data?.map((template) => <option key={template.id} value={template.id}>{template.title}</option>)}
      </select>
      <Button type="button" size="small" disabled={!current} onClick={() => {
        if (!current) return;
        insert(current.body.replaceAll("{nombre}", name).replaceAll("{numero}", number).replaceAll("{asunto}", subject));
        setSelected("");
      }}>Insertar</Button>
      <small>Se añade al borrador; revísala antes de enviar.</small>
      <ErrorBox error={templates.error} />
    </div>
  );
}

export function ReplyTemplatesAdmin() {
  const templates = useReplyTemplates(true);
  const client = useQueryClient();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<string | null>(null);
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [enabled, setEnabled] = useState(true);
  const [deleting, setDeleting] = useState<ReplyTemplate | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const [notice, setNotice] = useState("");
  async function refresh() {
    await client.invalidateQueries({ queryKey: ["reply-templates"] });
  }
  function edit(template?: ReplyTemplate) {
    setEditing(template?.id ?? null);
    setTitle(template?.title ?? "");
    setBody(template?.body ?? "");
    setEnabled(template?.enabled ?? true);
    setError(undefined);
    setOpen(true);
  }
  async function save() {
    setBusy(true); setError(undefined); setNotice("");
    try {
      await api(editing ? "/admin/reply-templates/" + editing : "/admin/reply-templates", {
        method: editing ? "PUT" : "POST", body: JSON.stringify({ title, body, enabled }),
      });
      setOpen(false);
      setNotice(editing ? "Plantilla actualizada." : "Plantilla creada.");
      await refresh();
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  async function changeEnabled(template: ReplyTemplate) {
    setBusy(true); setError(undefined); setNotice("");
    try {
      await api("/admin/reply-templates/" + template.id, {
        method: "PUT", body: JSON.stringify({ title: template.title, body: template.body, enabled: !template.enabled }),
      });
      await refresh();
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  async function remove() {
    if (!deleting) return;
    setBusy(true); setError(undefined); setNotice("");
    try {
      await api("/admin/reply-templates/" + deleting.id, { method: "DELETE" });
      setDeleting(null);
      setNotice("Plantilla eliminada.");
      await refresh();
    } catch (failure) { setError(failure); }
    finally { setBusy(false); }
  }
  return (
    <>
      <div className="page-heading">
        <div><span className="eyebrow">RESPONDER CON CLARIDAD</span><h1>Respuestas rápidas.</h1>
          <p>Prepara textos compartidos para que el equipo responda más rápido. Siempre podrán editarlos antes de enviarlos.</p></div>
        <Button variant="contained" startIcon={<Plus size={17} />} onClick={() => edit()}>Nueva plantilla</Button>
      </div>
      {notice && <Alert severity="success">{notice}</Alert>}
      <ErrorBox error={!open ? error : undefined} />
      <ErrorBox error={templates.error} />
      <section className="inbox-panel">
        <div className="panel-title"><h2>Catálogo <span className="count">{templates.data?.length ?? "—"}</span></h2></div>
        {templates.isPending ? <Loading /> : templates.data?.length ? (
          <div className="table-scroll"><table className="tickets-table admin-table"><thead><tr>
            <th>Nombre</th><th>Texto</th><th>Estado</th><th>Acciones</th>
          </tr></thead><tbody>{templates.data.map((template) => <tr key={template.id}>
            <td><strong>{template.title}</strong></td>
            <td className="reply-template-excerpt">{template.body}</td>
            <td>{template.enabled ? "Activa" : "Desactivada"}</td>
            <td><div className="heading-actions">
              <Button size="small" onClick={() => edit(template)}>Editar</Button>
              <Button size="small" disabled={busy} onClick={() => void changeEnabled(template)}>{template.enabled ? "Desactivar" : "Activar"}</Button>
              <Button size="small" color="error" onClick={() => setDeleting(template)}>Eliminar</Button>
            </div></td>
          </tr>)}</tbody></table></div>
        ) : <div className="empty"><MessageSquareText size={34} /><h3>Aún no hay plantillas</h3><p>Crea una para que los agentes puedan insertarla al responder.</p></div>}
      </section>
      <Dialog open={open} onClose={() => !busy && setOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editing ? "Editar plantilla" : "Nueva plantilla"}</DialogTitle>
        <DialogContent><div className="form-fields">
          <p className="form-note">Puedes usar {'{nombre}'}, {'{numero}'} y {'{asunto}'}. Se reemplazarán con los datos del ticket.</p>
          <TextField label="Nombre" required value={title} inputProps={{ maxLength: 80 }} onChange={(e) => setTitle(e.target.value)} disabled={busy} />
          <TextField label="Respuesta" required multiline minRows={7} value={body} inputProps={{ maxLength: 4000 }} onChange={(e) => setBody(e.target.value)} disabled={busy} />
          <label className="reply-template-enabled"><input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} /> Disponible para agentes</label>
          <ErrorBox error={open ? error : undefined} />
        </div></DialogContent>
        <DialogActions><Button disabled={busy} onClick={() => setOpen(false)}>Cancelar</Button>
          <Button variant="contained" disabled={busy || title.trim().length < 2 || body.trim().length < 5} onClick={() => void save()}>Guardar plantilla</Button></DialogActions>
      </Dialog>
      <Dialog open={!!deleting} onClose={() => !busy && setDeleting(null)}>
        <DialogTitle>Eliminar plantilla</DialogTitle>
        <DialogContent>¿Eliminar «{deleting?.title}» del catálogo? Esta acción no cambia las respuestas ya enviadas.
          <ErrorBox error={deleting ? error : undefined} />
        </DialogContent>
        <DialogActions><Button disabled={busy} onClick={() => setDeleting(null)}>Conservar</Button>
          <Button color="error" disabled={busy} onClick={() => void remove()}>Eliminar</Button></DialogActions>
      </Dialog>
    </>
  );
}
