import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Button, Dialog, DialogTitle, DialogContent, DialogActions, TextField, MenuItem, Checkbox, FormControlLabel } from "@mui/material";
import { api, type Directory } from "./api";
import { ErrorBox } from "./shared";

export function UserRoleEditor({ user, modules }: { user: NonNullable<Directory["users"]>[number]; modules: Directory["modules"] }) {
  const client = useQueryClient();
  const [open, setOpen] = useState(false);
  const [role, setRole] = useState(user.role);
  const [selected, setSelected] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  return <>
    <Button onClick={() => { setRole(user.role); setSelected(user.moduleIds || []); setError(undefined); setOpen(true); }}>Cambiar rol</Button>
    <Dialog open={open} onClose={() => !busy && setOpen(false)} fullWidth maxWidth="sm">
      <DialogTitle>Cambiar rol de usuario</DialogTitle>
      <DialogContent>
        <p>{user.displayName} · {user.email}</p>
        <TextField select fullWidth label="Rol" value={role} disabled={busy} onChange={e => setRole(e.target.value)} margin="normal">
          <MenuItem value="Requester">Solicitante</MenuItem>
          <MenuItem value="Agent">Agente</MenuItem>
          <MenuItem value="Admin">Administrador</MenuItem>
        </TextField>
        <p>{role === "Admin" ? "Tendrá acceso administrativo a usuarios, configuración y todos los tickets." : role === "Agent" ? "Podrá atender los tickets de los módulos seleccionados." : "Solo podrá consultar y responder sus propias solicitudes."}</p>
        {role === "Agent" && <div>{modules.map(m => <FormControlLabel key={m.id} label={m.category + " / " + m.name} control={<Checkbox disabled={busy} checked={selected.includes(m.id)} onChange={(_, checked) => setSelected(ids => checked ? [...ids, m.id] : ids.filter(id => id !== m.id))}/>}/>)}</div>}
        <p>Se conservarán su cuenta e historial. Tendrá que volver a iniciar sesión.</p>
        <ErrorBox error={error}/>
      </DialogContent>
      <DialogActions>
        <Button disabled={busy} onClick={() => setOpen(false)}>Cancelar</Button>
        <Button variant="contained" disabled={busy || role === user.role || (role === "Agent" && selected.length === 0)} onClick={async () => {
          setBusy(true); setError(undefined);
          try {
            await api("/admin/catalog/users/" + user.id + "/role", { method: "PUT", body: JSON.stringify({ role, moduleIds: role === "Agent" ? selected : [] }) });
            setOpen(false);
            await client.invalidateQueries({ queryKey: ["directory"] });
          } catch (e) { setError(e); } finally { setBusy(false); }
        }}>Confirmar cambio</Button>
      </DialogActions>
    </Dialog>
  </>;
}
