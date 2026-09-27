import { useState } from "react";
import { Link } from "react-router-dom";
import { Alert, Button, TextField } from "@mui/material";
import { api } from "./api";
import { Brand } from "./Brand";
import { ErrorBox } from "./shared";

export function ActivateAccount() {
  const [credentials] = useState(
    () => new URLSearchParams(window.location.hash.slice(1)),
  );
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState<unknown>();
  const valid = credentials.has("user") && credentials.has("token");
  return (
    <main
      className="account-card"
      style={{ maxWidth: 520, margin: "48px auto", padding: 32 }}
    >
      <Brand />
      <h1>Activa tu cuenta</h1>
      {done ? (
        <>
          <Alert severity="success">
            Tu contraseña quedó creada. Ya puedes ingresar con tu correo y
            contraseña.
          </Alert>
          <Button component={Link} to="/">
            Ir al inicio de sesión
          </Button>
        </>
      ) : (
        <>
          <p>Define tu contraseña para empezar a usar Sidecil Soporte.</p>
          {!valid ? (
            <Alert severity="error">
              Abre el enlace completo de tu invitación. Si venció, solicita una
              nueva al administrador.
            </Alert>
          ) : (
            <form
              className="form-fields"
              onSubmit={async (e) => {
                e.preventDefault();
                setError(undefined);
                if (password !== confirmation) {
                  setError(new Error("Las contraseñas no coinciden."));
                  return;
                }
                setBusy(true);
                try {
                  await api("/auth/invitation", {
                    method: "POST",
                    body: JSON.stringify({
                      userId: credentials.get("user"),
                      token: credentials.get("token"),
                      password,
                    }),
                  });
                  setPassword("");
                  setConfirmation("");
                  setDone(true);
                  window.history.replaceState(
                    null,
                    "",
                    window.location.pathname,
                  );
                } catch (e) {
                  setError(e);
                } finally {
                  setBusy(false);
                }
              }}
            >
              <TextField
                label="Nueva contraseña"
                type="password"
                autoComplete="new-password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                inputProps={{ minLength: 12, maxLength: 128 }}
                helperText="Mínimo 12 caracteres, mayúsculas, minúsculas, números y símbolos."
              />
              <TextField
                label="Confirmar contraseña"
                type="password"
                autoComplete="new-password"
                required
                value={confirmation}
                onChange={(e) => setConfirmation(e.target.value)}
                inputProps={{ minLength: 12, maxLength: 128 }}
              />
              <ErrorBox error={error} />
              <Button type="submit" variant="contained" disabled={busy}>
                {busy ? "Activando…" : "Crear mi contraseña"}
              </Button>
            </form>
          )}
        </>
      )}
    </main>
  );
}
