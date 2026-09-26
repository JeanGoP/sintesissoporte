import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, TextField, Switch } from "@mui/material";
import { Code2, MessageCircle, Plus, Copy, ExternalLink } from "lucide-react";
import { api } from "./api";
import { ErrorBox, Loading } from "./shared";
import "./chat.css";
type Site = { id: string; name: string; origin: string; enabled: boolean };
export function ChatSettings() {
  const sites = useQuery({
    queryKey: ["chat-sites"],
    queryFn: async () => (await api<Site[]>("/admin/chat/sites")).data,
  });
  const [name, setName] = useState("");
  const [origin, setOrigin] = useState("");
  const [selected, setSelected] = useState("");
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const [copied, setCopied] = useState(false);
  const site =
    sites.data?.find((s) => s.id === selected) ||
    sites.data?.find((s) => s.enabled) ||
    sites.data?.[0];
  const snippet = site
    ? `<script src="${location.origin}/sidecil-chat.js" data-site="${site.id}" defer></script>`
    : "";
  async function run(fn: () => Promise<void>) {
    setBusy(true);
    setError(undefined);
    try {
      await fn();
      await sites.refetch();
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  if (sites.isPending) return <Loading />;
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">SOPORTE DONDE LO NECESITAS</span>
          <h1>Chat en tus sistemas.</h1>
          <p>
            Recibe solicitudes desde tu ERP y conviértelas en tickets con su
            conversación y archivos.
          </p>
        </div>
        <MessageCircle size={32} />
      </div>
      <ErrorBox error={error || sites.error} />
      <Alert severity="info" sx={{ mb: 3 }}>
        Primera versión: recepción guiada y confirmación por correo. Las
        respuestas basadas en documentación y la atención en vivo se
        incorporarán después.
      </Alert>
      <div className="chat-admin-grid">
        <section className="chat-admin-card">
          <h2>
            <Plus size={19} /> Registrar un sistema
          </h2>
          <p>
            Usa el origen exacto donde se mostrará el chat, sin rutas. Por
            ejemplo: https://erp.sidecil.com.
          </p>
          <form
            className="chat-site-form"
            onSubmit={(e) => {
              e.preventDefault();
              void run(async () => {
                const created = (
                  await api<Site>("/admin/chat/sites", {
                    method: "POST",
                    body: JSON.stringify({ name, origin }),
                  })
                ).data;
                setSelected(created.id);
                setName("");
                setOrigin("");
              });
            }}
          >
            <TextField
              label="Nombre del sistema"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              slotProps={{ htmlInput: { minLength: 2, maxLength: 80 } }}
              disabled={busy}
            />
            <TextField
              label="Origen permitido"
              placeholder="https://erp.sidecil.com"
              type="url"
              required
              value={origin}
              onChange={(e) => setOrigin(e.target.value)}
              slotProps={{ htmlInput: { maxLength: 300 } }}
              disabled={busy}
            />
            <Button type="submit" variant="contained" disabled={busy}>
              Crear integración
            </Button>
          </form>
          <h2 className="chat-systems-heading">Sistemas conectados</h2>
          {!sites.data?.length && (
            <p>
              Aún no hay integraciones. Registra tu primer sistema para obtener
              el código.
            </p>
          )}
          {sites.data?.map((s) => (
            <div
              className={
                "chat-site-row " + (s.id === site?.id ? "selected" : "")
              }
              key={s.id}
            >
              <button
                className="chat-site-select"
                onClick={() => {
                  setSelected(s.id);
                  setCopied(false);
                }}
              >
                <strong>{s.name}</strong>
                <small>{s.origin}</small>
              </button>
              <Switch
                checked={s.enabled}
                disabled={busy}
                slotProps={{ input: { "aria-label": "Activar " + s.name } }}
                onChange={(_, enabled) =>
                  void run(async () => {
                    await api("/admin/chat/sites/" + s.id, {
                      method: "PUT",
                      body: JSON.stringify({ enabled }),
                    });
                  })
                }
              />
            </div>
          ))}
        </section>
        <section className="chat-admin-card">
          <h2>
            <Code2 size={19} /> Instalar y probar
          </h2>
          {site ? (
            <>
              <h3>{site.name}</h3>
              <p>
                Copia este código antes del cierre de la etiqueta body de tu
                sistema. Mostrará el botón «¿Necesitas ayuda?».
              </p>
              <pre className="chat-code">
                <code>{snippet}</code>
              </pre>
              <div className="chat-actions">
                <Button
                  startIcon={<Copy size={16} />}
                  onClick={async () => {
                    try {
                      await navigator.clipboard.writeText(snippet);
                      setCopied(true);
                    } catch {
                      setError(
                        new Error("Selecciona y copia el código manualmente."),
                      );
                    }
                  }}
                >
                  {copied ? "Código copiado" : "Copiar código"}
                </Button>
                <Button
                  startIcon={<ExternalLink size={16} />}
                  href={"/chat-demo.html?site=" + site.id}
                  target="_blank"
                  rel="noopener"
                >
                  Probar botón flotante
                </Button>
              </div>
              {location.protocol === "http:" && (
                <Alert severity="warning" sx={{ my: 2 }}>
                  La dirección actual es local. Para instalarlo en tu ERP
                  publicado, abre este panel desde el dominio HTTPS de Sidecil
                  Tickets y copia el código desde allí.
                </Alert>
              )}
              <p className="chat-caption">
                El chat solo se puede embeber en el origen registrado y en este
                portal. No recibe datos de sesión del ERP en esta versión.
              </p>
              {site.enabled ? (
                <iframe
                  key={site.id}
                  className="chat-preview"
                  title="Vista previa del chat"
                  src={"/chat-widget?site=" + site.id}
                />
              ) : (
                <Alert severity="warning">
                  Esta integración está desactivada.
                </Alert>
              )}
            </>
          ) : (
            <p>
              La vista previa y el código aparecerán cuando registres un
              sistema.
            </p>
          )}
        </section>
      </div>
    </>
  );
}
