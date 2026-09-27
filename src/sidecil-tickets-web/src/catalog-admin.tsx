import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  MenuItem,
  Checkbox,
  FormControlLabel,
  Alert,
} from "@mui/material";
import { api, type Directory } from "./api";
import { ErrorBox } from "./shared";
type Category = { id: string; name: string; enabled: boolean };
type Module = Category & { categoryId: string };
export function CatalogAdmin() {
  const client = useQueryClient();
  const data = useQuery({
    queryKey: ["catalog-admin"],
    queryFn: async () =>
      (
        await api<{ categories: Category[]; modules: Module[] }>(
          "/admin/catalog",
        )
      ).data,
  });
  const [edit, setEdit] = useState<{
    kind: "categories" | "modules";
    id?: string;
    name: string;
    enabled: boolean;
    categoryId: string;
  }>();
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState(false);
  return (
    <section className="inbox-panel" style={{ marginTop: 24 }}>
      <div className="panel-title">
        <h2>Categorías y módulos</h2>
        <Button
          onClick={() => {
            setError(undefined);
            setEdit({
              kind: "categories",
              name: "",
              enabled: true,
              categoryId: "",
            });
          }}
        >
          Nueva categoría
        </Button>
        <Button
          onClick={() => {
            setError(undefined);
            setEdit({
              kind: "modules",
              name: "",
              enabled: true,
              categoryId: "",
            });
          }}
        >
          Nuevo módulo
        </Button>
      </div>
      <p style={{ padding: 16 }}>
        Los agentes reciben solicitudes de sus módulos. Desactivar una categoría
        o módulo impide nuevas solicitudes y conserva los tickets existentes.
      </p>
      <ErrorBox error={data.error} />
      <div className="table-scroll">
        <table className="tickets-table admin-table">
          <thead>
            <tr>
              <th>Categoría</th>
              <th>Módulos</th>
              <th>Estado</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {data.data?.categories.map((c) => (
              <tr key={c.id}>
                <td>{c.name}</td>
                <td>
                  {data.data?.modules
                    .filter((m) => m.categoryId === c.id)
                    .map((m) => (
                      <Button
                        key={m.id}
                        onClick={() => {
                          setError(undefined);
                          setEdit({ ...m, kind: "modules" });
                        }}
                      >
                        {m.name}
                        {!m.enabled ? " (inactivo)" : ""}
                      </Button>
                    ))}
                </td>
                <td>{c.enabled ? "Activa" : "Inactiva"}</td>
                <td>
                  <Button
                    onClick={() => {
                      setError(undefined);
                      setEdit({ ...c, kind: "categories", categoryId: "" });
                    }}
                  >
                    Editar categoría
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Dialog
        open={!!edit}
        onClose={() => !busy && setEdit(undefined)}
        fullWidth
        maxWidth="sm"
      >
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            if (!edit) return;
            setBusy(true);
            setError(undefined);
            try {
              await api(
                "/admin/catalog/" + edit.kind + (edit.id ? "/" + edit.id : ""),
                {
                  method: edit.id ? "PUT" : "POST",
                  body: JSON.stringify(edit),
                },
              );
              setEdit(undefined);
              await client.invalidateQueries({ queryKey: ["catalog-admin"] });
              await client.invalidateQueries({ queryKey: ["directory"] });
            } catch (e) {
              setError(e);
            } finally {
              setBusy(false);
            }
          }}
        >
          <DialogTitle>
            {edit?.id ? "Editar" : "Crear"}{" "}
            {edit?.kind === "categories" ? "categoría" : "módulo"}
          </DialogTitle>
          <DialogContent>
            <div className="form-fields">
              <TextField
                required
                label="Nombre"
                value={edit?.name || ""}
                inputProps={{
                  minLength: 2,
                  maxLength: edit?.kind === "categories" ? 60 : 120,
                }}
                onChange={(e) =>
                  edit && setEdit({ ...edit, name: e.target.value })
                }
              />
              {edit?.kind === "modules" && (
                <TextField
                  select
                  required
                  label="Categoría del módulo"
                  value={edit.categoryId}
                  onChange={(e) =>
                    setEdit({ ...edit, categoryId: e.target.value })
                  }
                >
                  {data.data?.categories.map((c) => (
                    <MenuItem key={c.id} value={c.id}>
                      {c.name}
                    </MenuItem>
                  ))}
                </TextField>
              )}
              <FormControlLabel
                control={
                  <Checkbox
                    checked={edit?.enabled || false}
                    onChange={(e) =>
                      edit && setEdit({ ...edit, enabled: e.target.checked })
                    }
                  />
                }
                label="Activo"
              />
              <ErrorBox error={error} />
            </div>
          </DialogContent>
          <DialogActions>
            {edit?.id && (
              <Button
                color="error"
                disabled={busy}
                onClick={() => {
                  setError(undefined);
                  setDeleting(true);
                }}
              >
                Eliminar
              </Button>
            )}
            <Button disabled={busy} onClick={() => setEdit(undefined)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={busy} variant="contained">
              Guardar
            </Button>
          </DialogActions>
        </form>
      </Dialog>
      <Dialog
        open={deleting}
        onClose={() => !busy && setDeleting(false)}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>
          Eliminar {edit?.kind === "categories" ? "categoría" : "módulo"}
        </DialogTitle>
        <DialogContent>
          <p>
            ¿Eliminar «{edit?.name}»? Solo se permite si no tiene registros
            relacionados. Esta acción no se puede deshacer.
          </p>
          <ErrorBox error={error} />
        </DialogContent>
        <DialogActions>
          <Button disabled={busy} onClick={() => setDeleting(false)}>
            Cancelar
          </Button>
          <Button
            color="error"
            variant="contained"
            disabled={busy}
            onClick={async () => {
              if (!edit?.id) return;
              setBusy(true);
              setError(undefined);
              try {
                await api("/admin/catalog/" + edit.kind + "/" + edit.id, {
                  method: "DELETE",
                });
                setDeleting(false);
                setEdit(undefined);
                await client.invalidateQueries({ queryKey: ["catalog-admin"] });
                await client.invalidateQueries({ queryKey: ["directory"] });
              } catch (e) {
                setError(e);
              } finally {
                setBusy(false);
              }
            }}
          >
            Eliminar definitivamente
          </Button>
        </DialogActions>
      </Dialog>
    </section>
  );
}
export function UserScopeEditor({
  user,
  directory,
  onSaved,
}: {
  user: NonNullable<Directory["users"]>[number];
  directory: Directory;
  onSaved: () => void;
}) {
  const [open, setOpen] = useState(false),
    [modules, setModules] = useState<string[]>([]),
    [orgs, setOrgs] = useState<string[]>([]),
    [busy, setBusy] = useState(false),
    [error, setError] = useState<unknown>();
  return (
    <>
      <Button
        onClick={() => {
          setModules(user.moduleIds || []);
          setOrgs(user.organizationIds || []);
          setError(undefined);
          setOpen(true);
        }}
      >
        Empresas y módulos
      </Button>
      <Dialog open={open} onClose={() => !busy && setOpen(false)} fullWidth>
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            setBusy(true);
            setError(undefined);
            try {
              await api("/admin/catalog/users/" + user.id + "/scope", {
                method: "PUT",
                body: JSON.stringify({
                  moduleIds: modules,
                  organizationIds: orgs,
                }),
              });
              setOpen(false);
              onSaved();
            } catch (e) {
              setError(e);
            } finally {
              setBusy(false);
            }
          }}
        >
          <DialogTitle>Acceso de {user.displayName}</DialogTitle>
          <DialogContent>
            <div className="form-fields">
              <p>
                Empresa principal:{" "}
                {
                  directory.organizations?.find(
                    (o) => o.id === user.organizationId,
                  )?.name
                }
              </p>
              <TextField
                select
                label="Empresas adicionales"
                SelectProps={{ multiple: true }}
                value={orgs}
                onChange={(e) =>
                  setOrgs(
                    typeof e.target.value === "string"
                      ? e.target.value.split(",")
                      : e.target.value,
                  )
                }
              >
                {directory.organizations
                  ?.filter((o) => o.id !== user.organizationId)
                  .map((o) => (
                    <MenuItem key={o.id} value={o.id}>
                      {o.name}
                    </MenuItem>
                  ))}
              </TextField>
              {user.role === "Agent" && (
                <>
                  <TextField
                    select
                    label="Módulos del agente"
                    SelectProps={{ multiple: true }}
                    value={modules}
                    onChange={(e) =>
                      setModules(
                        typeof e.target.value === "string"
                          ? e.target.value.split(",")
                          : e.target.value,
                      )
                    }
                  >
                    {directory.modules?.map((m) => (
                      <MenuItem key={m.id} value={m.id}>
                        {m.category} / {m.name}
                      </MenuItem>
                    ))}
                  </TextField>
                  <Alert severity="info">
                    Sin módulos, el agente no ve tickets. Al retirar un módulo
                    se liberan sus asignaciones en ese módulo.
                  </Alert>
                </>
              )}
              <ErrorBox error={error} />
            </div>
          </DialogContent>
          <DialogActions>
            <Button disabled={busy} onClick={() => setOpen(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={busy} variant="contained">
              Guardar acceso
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </>
  );
}
