import { ActivateAccount } from "./activate-account";
import React, { useState, useEffect } from "react";
import { createRoot } from "react-dom/client";
import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
  NavLink,
} from "react-router-dom";
import {
  QueryClient,
  QueryClientProvider,
  useQuery,
} from "@tanstack/react-query";
import {
  ThemeProvider,
  createTheme,
  Button,
  TextField,
  CircularProgress,
} from "@mui/material";
import {
  ArrowRight,
  ArrowUpRight,
  Inbox,
  LayoutDashboard,
  LifeBuoy,
  LogOut,
  Plus,
  Mail as SendIcon,
  Settings2,
  ShieldCheck,
  Users,
  CheckCircle2,
} from "lucide-react";
import { api, ApiError, refreshCsrf, initials, type User } from "./api";
import { InboxPage, CreateDialog, ReportsPage } from "./tickets";
import { DetailPage } from "./detail";
import { AdminPage, AccountPage } from "./settings";
import { ErrorBox, Loading } from "./shared";
import "./styles.css";
import { Brand } from "./Brand";
import { ExternalLoginButtons } from "./external-login";
import { GuestPage, MailSettings } from "./mail";
import { ChatWidget } from "./chat";
import { ChatSettings } from "./chat-settings";
import { MessageCircle } from "lucide-react";
const theme = createTheme({
  palette: {
    primary: { main: "#3657db", dark: "#2442b8", contrastText: "#ffffff" },
    secondary: { main: "#00b9ed" },
  },
  typography: {
    fontFamily: '"Segoe UI", Arial, sans-serif',
    button: { textTransform: "none", fontWeight: 600 },
  },
  shape: { borderRadius: 10 },
});
const client = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (n, e) => !(e instanceof ApiError && e.status === 401) && n < 1,
      refetchOnWindowFocus: false,
    },
  },
});

function App() {
  useEffect(() => {
    const expired = () => {
      client.removeQueries({ predicate: (q) => q.queryKey[0] !== "me" });
      void client.resetQueries({ queryKey: ["me"] });
    };
    window.addEventListener("sidecil:session-expired", expired);
    return () => window.removeEventListener("sidecil:session-expired", expired);
  }, []);
  const me = useQuery({
    queryKey: ["me"],
    queryFn: async () => (await api<User>("/auth/me")).data,
    retry: false,
  });
  if (me.isPending) return <Loading />;
  if (!me.data) {
    if (me.error instanceof ApiError && me.error.status === 401)
      return (
        <Login
          onSuccess={async () => {
            await refreshCsrf();
            client.removeQueries({ predicate: (q) => q.queryKey[0] !== "me" });
            await me.refetch();
          }}
        />
      );
    return (
      <div className="connection-error">
        <Brand />
        <h1>No pudimos conectar con tu espacio</h1>
        <ErrorBox error={me.error} />
        <Button onClick={() => me.refetch()}>Reintentar</Button>
      </div>
    );
  }
  return <Shell user={me.data} />;
}
function Login({ onSuccess }: { onSuccess: () => Promise<void> }) {
  const [email, setEmail] = useState(""),
    [password, setPassword] = useState(""),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api("/auth/login", {
        method: "POST",
        body: JSON.stringify({ email, password }),
      });
      await onSuccess();
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="login">
      <section className="login-story">
        <Brand />
        <div>
          <span className="eyebrow light">
            PERSONAS PRIMERO. SOLUCIONES SIEMPRE.
          </span>
          <h1>
            Cada solicitud,
            <br />
            un paso adelante.
          </h1>
          <p>
            Un solo lugar para conectar con Sidecil, resolver lo que necesitas y
            seguir cada avance.
          </p>
          <div className="login-illustration">
            <span className="orb" />
            <div className="sample-card">
              <span className="sample-icon">
                <CheckCircle2 />
              </span>
              <div>
                <strong>Estamos para ayudarte</strong>
                <small>Tu equipo y tus clientes, conectados.</small>
              </div>
              <ArrowUpRight />
            </div>
          </div>
        </div>
        <span className="login-footer">SIDECIL · SOPORTE QUE CONECTA</span>
      </section>
      <section className="login-form">
        <div className="mobile-brand">
          <Brand />
        </div>
        <span className="eyebrow">TU ESPACIO SIDECIL</span>
        <h2>Qué bueno tenerte aquí.</h2>
        <p>Ingresa para gestionar tus solicitudes.</p>
        <ExternalLoginButtons />
        <form onSubmit={submit}>
          <TextField
            fullWidth
            label="Correo electrónico"
            type="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="username"
          />
          <TextField
            fullWidth
            label="Contraseña"
            type="password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
          />
          <ErrorBox error={error} />
          <Button
            fullWidth
            size="large"
            variant="contained"
            type="submit"
            disabled={busy}
            endIcon={
              busy ? <CircularProgress size={18} /> : <ArrowRight size={18} />
            }
          >
            Ingresar a mi espacio
          </Button>
        </form>
        <Button fullWidth href="/solicitar" variant="outlined" sx={{ mt: 2 }}>
          Crear ticket sin cuenta
        </Button>
        <div className="login-note">
          <ShieldCheck size={18} />
          <span>
            Acceso para empleados y clientes de Sidecil.
            <br />
            Si eres cliente, puedes crear tu cuenta al continuar con Google o
            Microsoft.
          </span>
        </div>
      </section>
    </div>
  );
}
function Shell({ user }: { user: User }) {
  const [create, setCreate] = useState(false),
    [error, setError] = useState<unknown>(),
    staff = user.role !== "Requester";
  async function logout() {
    try {
      await api("/auth/logout", { method: "POST" });
      client.clear();
      location.assign("/");
    } catch (e) {
      setError(e);
    }
  }
  return (
    <div className="shell">
      <aside className="sidebar">
        <Brand />
        <div className="workspace-tag">
          <span className="workspace-avatar">S</span>
          <div>
            <strong>Sidecil</strong>
            <small>
              {staff ? "Espacio de trabajo" : "Portal de solicitudes"}
            </small>
          </div>
          <ShieldCheck size={16} />
        </div>
        <div className="nav-label">PRINCIPAL</div>
        <nav>
          <NavLink to="/" end>
            <Inbox size={19} />
            Bandeja de tickets
          </NavLink>
          <NavLink to="/reports">
            <LayoutDashboard size={19} />
            {staff ? "Resumen operativo" : "Mi resumen"}
          </NavLink>
          {user.role === "Admin" && (
            <NavLink to="/admin" end>
              <Users size={19} />
              Personas y acceso
            </NavLink>
          )}
          {user.role === "Admin" && (
            <NavLink to="/admin/email">
              <SendIcon />
              Correo de atención
            </NavLink>
          )}
          {user.role === "Admin" && (
            <NavLink to="/admin/chat">
              <MessageCircle size={19} />
              Chat en tus sistemas
            </NavLink>
          )}
          <NavLink to="/account">
            <Settings2 size={19} />
            Mi cuenta
          </NavLink>
        </nav>
        <div className="sidebar-help">
          <span className="help-icon">
            <LifeBuoy size={21} />
          </span>
          <strong>Todo empieza por escuchar.</strong>
          <p>
            Cuéntanos qué necesitas.
            <br />
            Nosotros te acompañamos.
          </p>
          <button onClick={() => setCreate(true)}>
            Crear una solicitud <ArrowUpRight size={16} />
          </button>
        </div>
        <div className="profile">
          <span className="avatar">{initials(user.displayName)}</span>
          <div>
            <strong>{user.displayName}</strong>
            <small>
              {user.role === "Admin"
                ? "Administrador"
                : staff
                  ? "Agente de soporte"
                  : "Solicitante"}
            </small>
          </div>
          <button
            className="icon-button"
            title="Cerrar sesión"
            aria-label="Cerrar sesión"
            onClick={logout}
          >
            <LogOut size={17} />
          </button>
        </div>
      </aside>
      <div className="main-wrap">
        <header className="topbar">
          <div>
            <span className="topbar-muted">Sidecil</span>
            <span className="separator">/</span>
            <span>Centro de atención</span>
          </div>
          <div className="topbar-right">
            <span className="workspace-live">
              <i />
              Sesión iniciada
            </span>
            <span className="topbar-divider" />
            <span className="avatar small">{initials(user.displayName)}</span>
            <button
              className="mobile-logout icon-button"
              aria-label="Cerrar sesión móvil"
              onClick={logout}
            >
              <LogOut size={16} />
            </button>
          </div>
        </header>
        <main>
          <ErrorBox error={error} />
          <Routes>
            <Route
              path="/"
              element={
                <InboxPage user={user} onCreate={() => setCreate(true)} />
              }
            />
            <Route path="/tickets/:id" element={<DetailPage user={user} />} />
            <Route path="/reports" element={<ReportsPage user={user} />} />
            <Route
              path="/admin"
              element={
                user.role === "Admin" ? (
                  <AdminPage />
                ) : (
                  <Navigate to="/" replace />
                )
              }
            />
            <Route
              path="/admin/email"
              element={
                user.role === "Admin" ? (
                  <MailSettings />
                ) : (
                  <Navigate to="/" replace />
                )
              }
            />
            <Route
              path="/admin/chat"
              element={
                user.role === "Admin" ? (
                  <ChatSettings />
                ) : (
                  <Navigate to="/" replace />
                )
              }
            />
            <Route path="/account" element={<AccountPage user={user} />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </main>
        <footer className="main-footer">
          <span>Hecho para conectar personas y soluciones.</span>
          <span>Sidecil Tickets · v0.3</span>
        </footer>
      </div>
      <CreateDialog open={create} onClose={() => setCreate(false)} />
    </div>
  );
}
createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ThemeProvider theme={theme}>
      <QueryClientProvider client={client}>
        <BrowserRouter basename={import.meta.env.BASE_URL}>
          <Routes>
            <Route path="/chat-widget" element={<ChatWidget />} />
            <Route path="/solicitar" element={<GuestPage />} />
            <Route path="/activar-cuenta" element={<ActivateAccount />} />
            <Route
              path="/confirmar-solicitud"
              element={<GuestPage confirm />}
            />
            <Route path="*" element={<App />} />
          </Routes>
        </BrowserRouter>
      </QueryClientProvider>
    </ThemeProvider>
  </React.StrictMode>,
);
