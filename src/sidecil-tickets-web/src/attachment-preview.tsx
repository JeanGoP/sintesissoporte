import { useEffect, useState } from "react";
import { Alert, Button, IconButton, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle } from "@mui/material";
import { Download, Eye } from "lucide-react";
import { backendUrl } from "./backend";
import type { TicketDetail } from "./api";

type Attachment = TicketDetail["attachments"][number];
function mime(name: string) {
  switch (name.split(".").pop()?.toLowerCase()) {
    case "png": return "image/png";
    case "jpg": case "jpeg": return "image/jpeg";
    case "pdf": return "application/pdf";
    case "txt": return "text/plain";
    default: return undefined;
  }
}
function url(ticketId: string, file: Attachment) {
  return backendUrl("/api/v1/tickets/" + ticketId + "/attachments/" + file.id);
}
export function TicketAttachments({ ticketId, files }: { ticketId: string; files: Attachment[] }) {
  const [selected, setSelected] = useState<Attachment | null>(null);
  return <section className="ticket-attachments">
    <h3>Archivos del ticket</h3>
    {files.map(file => <div className="attachment-row" key={file.id}>
      <div className="attachment-name"><strong>{file.fileName}</strong><span>{(file.length / 1024).toFixed(1)} KB</span></div>
      <div className="attachment-actions">
        {mime(file.fileName) && <IconButton color="primary" title="Ver" onClick={() => setSelected(file)} aria-label={"Ver " + file.fileName}><Eye size={18}/></IconButton>}
        <IconButton color="primary" component="a" href={url(ticketId, file)} download title="Descargar" aria-label={"Descargar " + file.fileName}><Download size={18}/></IconButton>
      </div>
    </div>)}
    {selected && <AttachmentPreview key={ticketId + selected.id} ticketId={ticketId} file={selected} close={() => setSelected(null)}/>}
  </section>;
}
function AttachmentPreview({ ticketId, file, close }: { ticketId: string; file: Attachment; close: () => void }) {
  const [content, setContent] = useState<{ url?: string; text?: string }>();
  const [error, setError] = useState("");
  const type = mime(file.fileName);
  useEffect(() => {
    const abort = new AbortController();
    let objectUrl: string | undefined;
    async function load() {
      try {
        const response = await fetch(url(ticketId, file), { credentials: "include", cache: "no-store", signal: abort.signal });
        if (!response.ok) throw new Error(response.status === 401 || response.status === 403
          ? "Tu sesión expiró o no tienes permiso para ver este archivo."
          : "No se pudo abrir el archivo. Cierra la vista previa e inténtalo de nuevo.");
        const blob = await response.blob();
        if (type === "text/plain") {
          const text = await blob.text();
          if (!abort.signal.aborted) setContent({ text });
        } else if (!abort.signal.aborted) {
          objectUrl = URL.createObjectURL(new Blob([blob], { type }));
          setContent({ url: objectUrl });
        }
      } catch (e) {
        if (!abort.signal.aborted) setError(e instanceof Error ? e.message : "No se pudo abrir el archivo.");
      }
    }
    void load();
    return () => { abort.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [ticketId, file.id, type]);
  return <Dialog open onClose={close} fullWidth maxWidth="lg" aria-labelledby="attachment-preview-title">
    <DialogTitle id="attachment-preview-title" sx={{ overflowWrap: "anywhere" }}>{file.fileName}</DialogTitle>
    <DialogContent dividers className="attachment-preview">
      {error ? <Alert severity="error">{error}</Alert> : !content ? <div role="status" className="attachment-loading"><CircularProgress size={28}/> Cargando archivo…</div>
        : type === "text/plain" ? <pre>{content.text}</pre>
        : type === "application/pdf" ? <><iframe title={"Vista previa de " + file.fileName} src={content.url}/><p>Si tu navegador no muestra el PDF, utiliza Descargar.</p></>
        : <img src={content.url} alt={file.fileName}/>}
    </DialogContent>
    <DialogActions>
      <IconButton color="primary" component="a" href={url(ticketId, file)} download title="Descargar" aria-label="Descargar"><Download size={18}/></IconButton>
      <Button onClick={close}>Cerrar</Button>
    </DialogActions>
  </Dialog>;
}
