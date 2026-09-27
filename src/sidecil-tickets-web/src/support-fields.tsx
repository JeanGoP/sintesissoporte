import { Alert, MenuItem, TextField } from "@mui/material";
import type { SupportModule } from "./api";
export const externalOrganization = "91563faf-00a3-49ac-b2d0-0e8c702f4d41";
export function SupportFields({
  categories,
  modules,
  category,
  moduleId,
  companyName,
  organizationId,
  organizations,
  onChange,
  disabled = false,
}: {
  categories: string[];
  modules: SupportModule[];
  category: string;
  moduleId: string;
  companyName: string;
  organizationId?: string;
  organizations?: { id: string; name: string }[];
  disabled?: boolean;
  onChange: (value: {
    category: string;
    moduleId: string;
    companyName: string;
    organizationId?: string;
  }) => void;
}) {
  const org =
    organizationId || (organizations?.length === 1 ? organizations[0].id : "");
  const update = (
    patch: Partial<{
      category: string;
      moduleId: string;
      companyName: string;
      organizationId: string;
    }>,
  ) =>
    onChange({
      category,
      moduleId,
      companyName,
      organizationId: org,
      ...patch,
    });
  const choices = modules.filter((m) => m.category === category);
  return (
    <>
      {organizations && (
        <TextField
          select
          required
          label="Empresa"
          value={org}
          disabled={disabled}
          onChange={(e) => update({ organizationId: e.target.value })}
        >
          {organizations.map((o) => (
            <MenuItem key={o.id} value={o.id}>
              {o.id === externalOrganization
                ? "Otra empresa / indicar nombre"
                : o.name}
            </MenuItem>
          ))}
        </TextField>
      )}
      {(!organizations || org === externalOrganization) && (
        <TextField
          required
          label="Nombre de la empresa"
          value={companyName}
          disabled={disabled}
          inputProps={{ minLength: 2, maxLength: 120 }}
          onChange={(e) => update({ companyName: e.target.value })}
        />
      )}
      <TextField
        select
        required
        label="Categoría"
        value={categories.includes(category) ? category : ""}
        disabled={disabled}
        onChange={(e) => update({ category: e.target.value, moduleId: "" })}
      >
        {categories.map((c) => (
          <MenuItem value={c} key={c}>
            {c}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        select
        required
        label="Módulo"
        value={choices.some((m) => m.id === moduleId) ? moduleId : ""}
        disabled={disabled || !choices.length}
        onChange={(e) => update({ moduleId: e.target.value })}
      >
        {choices.map((m) => (
          <MenuItem value={m.id} key={m.id}>
            {m.name}
          </MenuItem>
        ))}
      </TextField>
      {category && !choices.length && (
        <Alert severity="info">
          Esta categoría aún no tiene módulos disponibles. Contacta al
          administrador.
        </Alert>
      )}
    </>
  );
}
