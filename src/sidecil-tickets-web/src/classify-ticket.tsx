import { useState } from "react";
import {
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  MenuItem,
} from "@mui/material";
import type { Directory, TicketDetail } from "./api";
export function ClassifyTicket({
  ticket,
  directory,
  save,
  busy,
}: {
  ticket: TicketDetail;
  directory: Directory;
  busy: boolean;
  save: (body: unknown) => Promise<boolean>;
}) {
  const [open, setOpen] = useState(false),
    [module, setModule] = useState(""),
    [org, setOrg] = useState(""),
    [company, setCompany] = useState("");
  return (
    <>
      <Button
        onClick={() => {
          setModule(ticket.moduleId || "");
          setOrg(ticket.organizationId || "");
          setCompany(
            ticket.organizationId === "91563faf-00a3-49ac-b2d0-0e8c702f4d41"
              ? ticket.organization
              : "",
          );
          setOpen(true);
        }}
      >
        Clasificar ticket
      </Button>
      <Dialog open={open} onClose={() => !busy && setOpen(false)} fullWidth>
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            if (
              await save({
                moduleId: module,
                organizationId: org || null,
                companyName:
                  org === "91563faf-00a3-49ac-b2d0-0e8c702f4d41"
                    ? company
                    : null,
              })
            )
              setOpen(false);
          }}
        >
          <DialogTitle>Empresa y módulo del ticket</DialogTitle>
          <DialogContent>
            <div className="form-fields">
              <TextField
                select
                required
                label="Empresa del ticket"
                value={org}
                onChange={(e) => setOrg(e.target.value)}
              >
                {directory.organizations?.map((o) => (
                  <MenuItem key={o.id} value={o.id}>
                    {o.name}
                  </MenuItem>
                ))}
              </TextField>
              {org === "91563faf-00a3-49ac-b2d0-0e8c702f4d41" && (
                <TextField
                  required
                  label="Nombre de la empresa"
                  value={company}
                  inputProps={{ minLength: 2, maxLength: 120 }}
                  onChange={(e) => setCompany(e.target.value)}
                />
              )}
              <TextField
                select
                required
                label="Categoría / módulo"
                value={module}
                onChange={(e) => setModule(e.target.value)}
              >
                {directory.modules?.map((m) => (
                  <MenuItem key={m.id} value={m.id}>
                    {m.category} / {m.name}
                  </MenuItem>
                ))}
              </TextField>
            </div>
          </DialogContent>
          <DialogActions>
            <Button disabled={busy} onClick={() => setOpen(false)}>
              Cancelar
            </Button>
            <Button disabled={busy} type="submit" variant="contained">
              Guardar clasificación
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </>
  );
}
