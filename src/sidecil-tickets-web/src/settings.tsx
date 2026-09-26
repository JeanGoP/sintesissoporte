import { ExternalConnections } from "./external-login";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  MenuItem,
  Alert,
} from "@mui/material";
import { Plus } from "lucide-react";
import { api, refreshCsrf, initials, type Directory, type User } from "./api";
import { ErrorBox } from "./shared";
export function AdminPage() {
  const directory = useQuery({
    queryKey: ["directory"],
    queryFn: async () => (await api<Directory>("/directory")).data,
  });
  const [open, setOpen] = useState(false),
    [orgOpen, setOrgOpen] = useState(false),
    [name, setName] = useState(""),
    [email, setEmail] = useState(""),
    [password, setPassword] = useState(""),
    [role, setRole] = useState("Requester"),
    [org, setOrg] = useState(""),
    [team, setTeam] = useState(""),
    [orgName, setOrgName] = useState(""),
    [error, setError] = useState<unknown>(),
    [busy, setBusy] = useState(false);
  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api("/admin/users", {
        method: "POST",
        body: JSON.stringify({
          displayName: name,
          email,
          password,
          role,
          organizationId: org,
          teamId: team || null,
        }),
      });
      setOpen(false);
      setName("");
      setEmail("");
      setPassword("");
      await directory.refetch();
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">UN ESPACIO, LAS PERSONAS CORRECTAS</span>
          <h1>Personas y acceso.</h1>
          <p>Gestiona las cuentas de empleados, clientes y agentes.</p>
        </div>
        <div className="heading-actions">
          <Button
            variant="outlined"
            onClick={() => {
              setOrgOpen(true);
              setError(undefined);
            }}
          >
            Nueva organización
          </Button>
          <Button
            variant="contained"
            startIcon={<Plus size={17} />}
            onClick={() => {
              setOpen(true);
              setError(undefined);
            }}
          >
            Crear usuario
          </Button>
        </div>
      </div>
      <ErrorBox error={directory.error} />
      <section className="inbox-panel">
        <div className="panel-title">
          <h2>
            Usuarios{" "}
            <span className="count">{directory.data?.users?.length || 0}</span>
          </h2>
        </div>
        <div className="table-scroll">
          <table className="tickets-table">
            <thead>
              <tr>
                <th>Persona</th>
                <th>Correo electrónico</th>
                <th>Rol</th>
              </tr>
            </thead>
            <tbody>
              {directory.data?.users?.map((u) => (
                <tr key={u.id}>
                  <td>
                    <div className="person">
                      <span className="avatar">{initials(u.displayName)}</span>
                      <strong>{u.displayName}</strong>
                    </div>
                  </td>
                  <td>{u.email}</td>
                  <td>
                    <span className="badge">
                      {u.role === "Admin"
                        ? "Administrador"
                        : u.role === "Agent"
                          ? "Agente"
                          : "Solicitante"}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
      <Dialog
        open={open}
        onClose={() => !busy && setOpen(false)}
        fullWidth
        maxWidth="sm"
      >
        <form onSubmit={submit}>
          <DialogTitle>Crear usuario</DialogTitle>
          <DialogContent>
            <div className="form-fields">
              <TextField
                label="Nombre completo"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                inputProps={{ maxLength: 120 }}
              />
              <TextField
                label="Correo electrónico"
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
              <TextField
                label="Contraseña inicial"
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                helperText="Mínimo 12 caracteres, mayúsculas, minúsculas, números y símbolos."
                inputProps={{ minLength: 12 }}
              />
              <TextField
                select
                label="Organización"
                required
                value={org}
                onChange={(e) => setOrg(e.target.value)}
              >
                {directory.data?.organizations?.map((o) => (
                  <MenuItem key={o.id} value={o.id}>
                    {o.name}
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                label="Rol"
                value={role}
                onChange={(e) => setRole(e.target.value)}
              >
                <MenuItem value="Requester">Solicitante</MenuItem>
                <MenuItem value="Agent">Agente</MenuItem>
                <MenuItem value="Admin">Administrador</MenuItem>
              </TextField>
              {role === "Agent" && (
                <TextField
                  select
                  label="Equipo"
                  required
                  value={team}
                  onChange={(e) => setTeam(e.target.value)}
                >
                  {directory.data?.teams?.map((t) => (
                    <MenuItem key={t.id} value={t.id}>
                      {t.name}
                    </MenuItem>
                  ))}
                </TextField>
              )}
              <ErrorBox error={error} />
            </div>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setOpen(false)} disabled={busy}>
              Cancelar
            </Button>
            <Button variant="contained" type="submit" disabled={busy}>
              Crear usuario
            </Button>
          </DialogActions>
        </form>
      </Dialog>
      <Dialog
        open={orgOpen}
        onClose={() => !busy && setOrgOpen(false)}
        fullWidth
        maxWidth="xs"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            setBusy(true);
            setError(undefined);
            try {
              await api("/admin/organizations", {
                method: "POST",
                body: JSON.stringify({ name: orgName }),
              });
              setOrgOpen(false);
              setOrgName("");
              await directory.refetch();
            } catch (e) {
              setError(e);
            } finally {
              setBusy(false);
            }
          }}
        >
          <DialogTitle>Nueva organización</DialogTitle>
          <DialogContent>
            <div className="form-fields">
              <TextField
                required
                label="Nombre de la organización"
                value={orgName}
                onChange={(e) => setOrgName(e.target.value)}
                inputProps={{ minLength: 2, maxLength: 120 }}
              />
              <ErrorBox error={error} />
            </div>
          </DialogContent>
          <DialogActions>
            <Button disabled={busy} onClick={() => setOrgOpen(false)}>
              Cancelar
            </Button>
            <Button disabled={busy} type="submit" variant="contained">
              Crear organización
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </>
  );
}
export function AccountPage({ user }: { user: User }) {
  const [currentPassword, setCurrent] = useState(""),
    [newPassword, setNew] = useState(""),
    [error, setError] = useState<unknown>(),
    [success, setSuccess] = useState(false),
    [busy, setBusy] = useState(false);
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">TU PERFIL</span>
          <h1>Mi cuenta.</h1>
          <p>
            {user.displayName} · {user.organization}
          </p>
        </div>
      </div>
      <ExternalConnections />
      <section className="account-card">
        <h2>Seguridad de acceso</h2>
        <p>{user.email}</p>
        <form
          className="form-fields"
          onSubmit={async (e) => {
            e.preventDefault();
            setBusy(true);
            setError(undefined);
            setSuccess(false);
            try {
              await api("/auth/password", {
                method: "POST",
                body: JSON.stringify({ currentPassword, newPassword }),
              });
              await refreshCsrf();
              setCurrent("");
              setNew("");
              setSuccess(true);
            } catch (e) {
              setError(e);
            } finally {
              setBusy(false);
            }
          }}
        >
          <TextField
            required
            label="Contraseña actual"
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(e) => setCurrent(e.target.value)}
          />
          <TextField
            required
            label="Nueva contraseña"
            type="password"
            autoComplete="new-password"
            inputProps={{ minLength: 12 }}
            value={newPassword}
            onChange={(e) => setNew(e.target.value)}
            helperText="Mínimo 12 caracteres, mayúsculas, minúsculas, números y símbolos."
          />
          <ErrorBox error={error} />
          {success && <Alert severity="success">Contraseña actualizada.</Alert>}
          <Button type="submit" variant="contained" disabled={busy}>
            Actualizar contraseña
          </Button>
        </form>
      </section>
    </>
  );
}
