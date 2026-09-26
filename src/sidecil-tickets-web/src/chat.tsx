import { useEffect, useRef, useState } from "react";
import { Alert, Button, TextField, MenuItem } from "@mui/material";
import {
  MessageCircle,
  Send,
  Paperclip,
  Trash2,
  CheckCircle2,
  ArrowRight,
} from "lucide-react";
import { Brand } from "./Brand";
import "./chat.css";

type Draft = {
  name: string;
  email: string;
  module: string;
  subject: string;
  body: string;
  category: string;
};
type Attachment = { id: string; fileName: string; length: number };
type Snapshot = Draft & {
  submitted: boolean;
  number: string | null;
  attachments: Attachment[];
};
type Session = { id: string; token: string };
const empty: Draft = {
  name: "",
  email: "",
  module: "",
  subject: "",
  body: "",
  category: "General",
};
const questions: {
  key: keyof Draft;
  text: string;
  label: string;
  min: number;
  max: number;
}[] = [
  {
    key: "name",
    text: "Hola, soy el asistente de recepción de Sidecil. Te ayudaré a preparar tu solicitud. ¿Cómo te llamas?",
    label: "Tu nombre",
    min: 2,
    max: 120,
  },
  {
    key: "email",
    text: "Gracias. ¿A qué correo enviamos la confirmación? No necesitas crear una cuenta.",
    label: "Tu correo",
    min: 5,
    max: 200,
  },
  {
    key: "module",
    text: "¿En qué módulo o parte del sistema necesitas ayuda?",
    label: "Módulo o pantalla",
    min: 2,
    max: 120,
  },
  {
    key: "subject",
    text: "Resume el problema en una frase.",
    label: "Asunto de la solicitud",
    min: 5,
    max: 180,
  },
  {
    key: "body",
    text: "Cuéntanos qué ocurrió y qué esperabas que pasara. En el siguiente paso podrás adjuntar capturas o documentos. Evita incluir contraseñas.",
    label: "Descripción del problema",
    min: 10,
    max: 8000,
  },
];
function nextStep(draft: Draft) {
  const index = questions.findIndex((q) => draft[q.key].length < q.min);
  return index < 0 ? questions.length : index;
}
export function ChatWidget() {
  const site = new URLSearchParams(location.search).get("site") || "";
  const storageKey = "sidecil-chat:" + site;
  const [config, setConfig] = useState<{
    name: string;
    available: boolean;
    testMode: boolean;
  }>();
  const [session, setSession] = useState<Session>();
  const [draft, setDraft] = useState<Draft>({ ...empty });
  const [step, setStep] = useState(0);
  const [files, setFiles] = useState<Attachment[]>([]);
  const [submitted, setSubmitted] = useState(false);
  const [number, setNumber] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const scroll = useRef<HTMLDivElement>(null);
  async function request<T>(
    path: string,
    options: RequestInit = {},
    active?: Session,
  ): Promise<T> {
    const response = await fetch("/api/v1/chat" + path, {
      ...options,
      credentials: "omit",
      headers: {
        "X-Sidecil-Widget": "1",
        "Content-Type": "application/json",
        ...(active ? { Authorization: "Bearer " + active.token } : {}),
        ...options.headers,
      },
    });
    if (!response.ok) {
      const p = await response.json().catch(() => ({}));
      throw new Error(
        p.detail ||
          (response.status === 401
            ? "Tu conversación venció o el chat fue desactivado. Puedes iniciar una nueva solicitud."
            : response.status === 429
              ? "Recibimos demasiadas solicitudes. Espera un momento e inténtalo nuevamente."
              : "No pudimos conectar con el chat. Inténtalo nuevamente."),
      );
    }
    return response.status === 204 ? (undefined as T) : response.json();
  }
  function apply(data: Snapshot) {
    setDraft(data);
    setStep(nextStep(data));
    setFiles(data.attachments);
    setSubmitted(data.submitted);
    setNumber(data.number);
  }
  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const info = await request<typeof config>("/sites/" + site);
        if (!active) return;
        setConfig(info);
        let saved: Session | undefined;
        try {
          saved =
            JSON.parse(sessionStorage.getItem(storageKey) || "null") ||
            undefined;
        } catch {
          /* Storage may be unavailable in an embedded browser. */
        }
        if (saved) {
          const data = await request<Snapshot>(
            "/sessions/" + saved.id,
            {},
            saved,
          );
          if (active) {
            setSession(saved);
            apply(data);
          }
        }
      } catch (e) {
        if (active) setError((e as Error).message);
      } finally {
        if (active) setReady(true);
      }
    })();
    return () => {
      active = false;
    };
  }, [site]);
  useEffect(() => {
    scroll.current?.scrollTo({
      top: scroll.current.scrollHeight,
      behavior: "smooth",
    });
  }, [step, submitted, number]);
  useEffect(() => {
    if (!session || !submitted || number) return;
    let stopped = false;
    const timer = setInterval(async () => {
      if (document.hidden) return;
      try {
        const data = await request<Snapshot>(
          "/sessions/" + session.id,
          {},
          session,
        );
        if (!stopped) setNumber(data.number);
      } catch {
        /* Manual refresh surfaces transient failures. */
      }
    }, 15000);
    return () => {
      stopped = true;
      clearInterval(timer);
    };
  }, [session, submitted, number]);
  async function action(fn: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await fn();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function start() {
    await action(async () => {
      const value = await request<Session>("/sessions", {
        method: "POST",
        body: JSON.stringify({ siteId: site }),
      });
      try {
        sessionStorage.setItem(storageKey, JSON.stringify(value));
      } catch {
        /* Continue in memory when third-party storage is blocked. */
      }
      setSession(value);
      setDraft({ ...empty });
      setStep(0);
      setFiles([]);
      setSubmitted(false);
      setNumber(null);
    });
  }
  async function save() {
    await request(
      "/sessions/" + session!.id + "/draft",
      { method: "PUT", body: JSON.stringify(draft) },
      session,
    );
  }
  const question = questions[step];
  return (
    <div className="chat-widget">
      <header className="chat-header">
        <Brand />
        <div>
          <span className="chat-online" /> Soporte · {config?.name || "Sidecil"}
        </div>
      </header>
      <div className="chat-scroll" ref={scroll}>
        {config?.testMode && (
          <Alert severity="info">
            Demostración: los correos se guardan localmente.
          </Alert>
        )}
        {!session && (
          <div className="chat-welcome">
            <span className="chat-welcome-icon">
              <MessageCircle size={32} />
            </span>
            <h1>Estamos para ayudarte.</h1>
            <p>
              Cuéntanos qué necesitas. Prepararemos una solicitud con tus
              archivos para nuestro equipo de soporte.
            </p>
            <p className="chat-caption">
              Asistente guiado · Sin cuenta · Confirmación por correo
            </p>
            <Button
              variant="contained"
              disabled={!ready || !config?.available || busy}
              onClick={start}
              endIcon={<ArrowRight size={17} />}
            >
              Iniciar solicitud
            </Button>
            {config && !config.available && (
              <p>El canal de recepción aún no está habilitado.</p>
            )}
          </div>
        )}
        {session && !submitted && (
          <>
            <div className="chat-transcript" aria-live="polite">
              {questions
                .slice(0, Math.min(step + 1, questions.length))
                .map((q, i) => (
                  <div key={q.key}>
                    <div className="chat-bubble assistant">{q.text}</div>
                    {i < step && (
                      <div className="chat-bubble customer">{draft[q.key]}</div>
                    )}
                  </div>
                ))}
            </div>
            {question ? (
              <form
                className="chat-answer"
                onSubmit={(e) => {
                  e.preventDefault();
                  void action(async () => {
                    await save();
                    setStep(step + 1);
                  });
                }}
              >
                <TextField
                  key={question.key}
                  autoFocus
                  fullWidth
                  required
                  label={question.label}
                  type={question.key === "email" ? "email" : "text"}
                  multiline={question.key === "body"}
                  minRows={question.key === "body" ? 4 : undefined}
                  value={draft[question.key]}
                  onChange={(e) =>
                    setDraft({ ...draft, [question.key]: e.target.value })
                  }
                  slotProps={{
                    htmlInput: {
                      minLength: question.min,
                      maxLength: question.max,
                    },
                  }}
                  disabled={busy}
                />
                <div className="chat-actions">
                  {step > 0 && (
                    <Button disabled={busy} onClick={() => setStep(step - 1)}>
                      Atrás
                    </Button>
                  )}
                  <Button
                    type="submit"
                    variant="contained"
                    disabled={
                      busy || draft[question.key].trim().length < question.min
                    }
                    endIcon={<ArrowRight size={16} />}
                  >
                    Continuar
                  </Button>
                </div>
              </form>
            ) : (
              <div className="chat-review">
                <h2>Revisa tu solicitud</h2>
                <dl>
                  <dt>Nombre</dt>
                  <dd>{draft.name}</dd>
                  <dt>Correo</dt>
                  <dd>{draft.email}</dd>
                  <dt>Módulo</dt>
                  <dd>{draft.module}</dd>
                  <dt>Asunto</dt>
                  <dd>{draft.subject}</dd>
                  <dt>Descripción</dt>
                  <dd>{draft.body}</dd>
                </dl>
                <TextField
                  select
                  fullWidth
                  label="Categoría"
                  value={draft.category}
                  disabled={busy}
                  onChange={(e) =>
                    setDraft({ ...draft, category: e.target.value })
                  }
                >
                  {[
                    "General",
                    "Soporte técnico",
                    "Facturación",
                    "Accesos",
                    "Servicios",
                  ].map((x) => (
                    <MenuItem key={x} value={x}>
                      {x}
                    </MenuItem>
                  ))}
                </TextField>
                <div className="chat-files">
                  <h3>Archivos de soporte</h3>
                  <p>Hasta 3 archivos de 5 MB: PNG, JPG, PDF o TXT.</p>
                  {files.map((file) => (
                    <div className="chat-file" key={file.id}>
                      <Paperclip size={16} />
                      <span>
                        {file.fileName}
                        <small>{(file.length / 1024).toFixed(1)} KB</small>
                      </span>
                      <button
                        type="button"
                        aria-label={"Eliminar " + file.fileName}
                        disabled={busy}
                        onClick={() =>
                          void action(async () => {
                            await request(
                              "/sessions/" +
                                session.id +
                                "/attachments/" +
                                file.id,
                              { method: "DELETE" },
                              session,
                            );
                            setFiles(files.filter((x) => x.id !== file.id));
                          })
                        }
                      >
                        <Trash2 size={17} />
                      </button>
                    </div>
                  ))}
                  <Button
                    component="label"
                    disabled={busy || files.length >= 3}
                    startIcon={<Paperclip size={16} />}
                  >
                    Adjuntar archivo
                    <input
                      type="file"
                      hidden
                      accept=".png,.jpg,.jpeg,.pdf,.txt"
                      onChange={(e) => {
                        const file = e.target.files?.[0];
                        e.target.value = "";
                        if (!file) return;
                        if (file.size > 5 * 1024 * 1024 || !file.size) {
                          setError(
                            "El archivo debe pesar entre 1 byte y 5 MB.",
                          );
                          return;
                        }
                        void action(async () => {
                          const added = await request<Attachment>(
                            "/sessions/" + session.id + "/attachments",
                            {
                              method: "POST",
                              headers: {
                                "Content-Type": "application/octet-stream",
                                "X-File-Name": encodeURIComponent(file.name),
                              },
                              body: file,
                            },
                            session,
                          );
                          setFiles([...files, added]);
                        });
                      }}
                    />
                  </Button>
                </div>
                <p className="chat-caption">
                  Al enviar, recibirás un enlace para verificar tu correo y
                  crear el ticket. La conversación y los archivos se compartirán
                  con el equipo de atención.
                </p>
                <Button
                  fullWidth
                  variant="contained"
                  endIcon={<Send size={16} />}
                  disabled={busy}
                  onClick={() =>
                    void action(async () => {
                      await save();
                      await request(
                        "/sessions/" + session.id + "/submit",
                        { method: "POST", body: "{}" },
                        session,
                      );
                      setSubmitted(true);
                    })
                  }
                >
                  Confirmar y enviar solicitud
                </Button>
                <Button fullWidth disabled={busy} onClick={() => setStep(0)}>
                  Corregir datos
                </Button>
              </div>
            )}
          </>
        )}
        {submitted && (
          <div className="chat-complete" aria-live="polite">
            <CheckCircle2 size={42} />
            <h1>{number ? "Tu ticket está creado." : "Revisa tu correo."}</h1>
            {number ? (
              <>
                <strong className="chat-ticket-number">{number}</strong>
                <p>
                  El equipo recibió tu conversación y los archivos. Puedes
                  agregar información respondiendo al correo del ticket.
                </p>
              </>
            ) : (
              <>
                <p>
                  Enviamos un enlace a <strong>{draft.email}</strong>. Ábrelo y
                  confirma la solicitud para crear el ticket.
                </p>
                <Button
                  disabled={busy}
                  onClick={() =>
                    void action(async () => {
                      const data = await request<Snapshot>(
                        "/sessions/" + session!.id,
                        {},
                        session,
                      );
                      setNumber(data.number);
                    })
                  }
                >
                  Ya confirmé mi correo
                </Button>
              </>
            )}
            <p className="chat-caption">
              La atención continuará por correo. Esta versión recibe
              solicitudes; todavía no responde consultas con IA.
            </p>
            <Button disabled={busy} onClick={start}>
              Nueva solicitud
            </Button>
          </div>
        )}
        {error && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error}
          </Alert>
        )}
      </div>
      <footer className="chat-footer">
        Atención de Sidecil · Tu solicitud en buenas manos
      </footer>
    </div>
  );
}
