import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, TextField } from "@mui/material";
import { api, refreshCsrf } from "./api";
import { ErrorBox } from "./shared";
type Provider = { name: string; enabled: boolean };
export function ExternalLoginButtons() {
  const providers = useQuery({
    queryKey: ["login-providers"],
    queryFn: async () => (await api<Provider[]>("/auth/providers")).data,
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const result = new URLSearchParams(location.search).get("external");
  return (
    <div className="external-login">
      {result === "unlinked" && (
        <Alert severity="info">
          Esta cuenta aún no está vinculada. Ingresa con tu correo y contraseña
          y vincúlala desde Mi cuenta. Si no tienes acceso, contacta al
          administrador.
        </Alert>
      )}
      {result === "failed" && (
        <Alert severity="error">
          No se completó el acceso externo. Inténtalo nuevamente o ingresa con
          tu contraseña.
        </Alert>
      )}
      <div className="external-buttons">
        {(providers.data || []).map((provider) => (
          <Button
            key={provider.name}
            variant="outlined"
            fullWidth
            disabled={busy || !provider.enabled}
            onClick={async () => {
              setBusy(true);
              setError(undefined);
              try {
                const result = (
                  await api<{ url: string }>(
                    "/auth/external/" + provider.name,
                    { method: "POST", body: "{}" },
                  )
                ).data;
                location.assign(result.url);
              } catch (e) {
                setError(e);
                setBusy(false);
              }
            }}
          >
            Continuar con {provider.name}
          </Button>
        ))}
      </div>
      {providers.data?.every((p) => !p.enabled) && (
        <p className="external-caption">
          El acceso con Microsoft y Google estará disponible cuando el
          administrador lo habilite.
        </p>
      )}
      <ErrorBox error={error || providers.error} />
      <p className="external-divider">O ingresa con tu correo y contraseña</p>
    </div>
  );
}
export function ExternalConnections() {
  const connections = useQuery({
    queryKey: ["external-connections"],
    queryFn: async () =>
      (
        await api<{ providers: Provider[]; linked: string[] }>(
          "/auth/connections",
        )
      ).data,
  });
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const [notice, setNotice] = useState("");
  const result = new URLSearchParams(location.search).get("external");
  async function change(provider: string, remove: boolean) {
    setBusy(true);
    setError(undefined);
    setNotice("");
    try {
      const response = await api<{ url: string }>(
        "/auth/connections/" + provider + (remove ? "/remove" : ""),
        { method: "POST", body: JSON.stringify({ password }) },
      );
      setPassword("");
      if (remove) {
        await refreshCsrf();
        await connections.refetch();
        setNotice("Cuenta desvinculada.");
      } else location.assign(response.data.url);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="account-card external-connections">
      <h2>Acceso con Microsoft y Google</h2>
      <p>
        Vincula una cuenta para ingresar desde la pantalla principal. Conservas
        tu rol, organización y acceso a los mismos tickets.
      </p>
      {result === "linked" && (
        <Alert severity="success">
          Cuenta vinculada. Ya puedes usarla para ingresar al sistema.
        </Alert>
      )}
      {result === "conflict" && (
        <Alert severity="warning">
          Esta cuenta ya está vinculada. No se cambió ninguna asociación.
        </Alert>
      )}
      {notice && <Alert severity="success">{notice}</Alert>}
      <TextField
        fullWidth
        type="password"
        label="Contraseña para vincular o desvincular"
        autoComplete="current-password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        disabled={busy}
        sx={{ mt: 2 }}
      />
      {connections.data?.providers.map((provider) => {
        const linked = connections.data.linked.includes(provider.name);
        return (
          <div className="external-connection" key={provider.name}>
            <span>
              <strong>{provider.name}</strong>
              <small>
                {linked
                  ? "Vinculada"
                  : provider.enabled
                    ? "Disponible para vincular"
                    : "Pendiente de configuración en el servidor"}
              </small>
            </span>
            <Button
              disabled={busy || !password || (!linked && !provider.enabled)}
              onClick={() => void change(provider.name, linked)}
            >
              {linked ? "Desvincular" : "Vincular"}
            </Button>
          </div>
        );
      })}
      <ErrorBox error={error || connections.error} />
    </section>
  );
}
