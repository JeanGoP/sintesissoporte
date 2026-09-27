import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
} from "@mui/material";
import { api, type Directory } from "./api";
import { ErrorBox } from "./shared";
export function AgentLifecycleActions({
  user,
}: {
  user: NonNullable<Directory["users"]>[number];
}) {
  const client = useQueryClient();
  const [action, setAction] = useState<"delete" | "toggle">();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>();
  const active = user.isActive !== false;
  if (user.role !== "Agent") return null;
  return (
    <>
      <span className="badge">{active ? "Activo" : "Inactivo"}</span>
      <Button
        onClick={() => {
          setError(undefined);
          setAction("toggle");
        }}
      >
        {active ? "Desactivar" : "Reactivar"}
      </Button>
      <Button
        color="error"
        onClick={() => {
          setError(undefined);
          setAction("delete");
        }}
      >
        Eliminar agente
      </Button>
      <Dialog
        open={!!action}
        onClose={() => !busy && setAction(undefined)}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>
          {action === "delete"
            ? "Eliminar agente"
            : active
              ? "Desactivar agente"
              : "Reactivar agente"}
        </DialogTitle>
        <DialogContent>
          <p>{user.displayName}</p>
          <p>
            {action === "delete"
              ? "Solo se eliminará si no tiene historial en tickets o conversaciones. Esta acción no se puede deshacer."
              : active
                ? "Se cerrarán sus sesiones y se liberarán sus tickets pendientes. Su historial y módulos se conservarán."
                : "Podrá volver a ingresar y atender los módulos asignados. Los tickets liberados no se reasignan automáticamente."}
          </p>
          <ErrorBox error={error} />
        </DialogContent>
        <DialogActions>
          <Button disabled={busy} onClick={() => setAction(undefined)}>
            Cancelar
          </Button>
          <Button
            color={action === "delete" ? "error" : "primary"}
            variant="contained"
            disabled={busy}
            onClick={async () => {
              setBusy(true);
              setError(undefined);
              try {
                await api(
                  "/admin/catalog/users/" +
                    user.id +
                    (action === "toggle" ? "/enabled" : ""),
                  {
                    method: action === "delete" ? "DELETE" : "PUT",
                    ...(action === "toggle"
                      ? { body: JSON.stringify({ enabled: !active }) }
                      : {}),
                  },
                );
                setAction(undefined);
                await client.invalidateQueries({ queryKey: ["directory"] });
                await client.invalidateQueries({ queryKey: ["tickets"] });
              } catch (e) {
                setError(e);
              } finally {
                setBusy(false);
              }
            }}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
