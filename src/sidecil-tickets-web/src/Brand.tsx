export function Brand() {
  return (
    <div className="brand">
      <img
        src={import.meta.env.BASE_URL + "branding/sidecil-logo.png"}
        alt="Sidecil · Cloud technology"
        width="623"
        height="264"
      />
    </div>
  );
}
