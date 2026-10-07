export function ClientesPage() {
  return (
    <div className="min-h-[calc(100vh-4rem)] bg-[#f5f3ef] px-4 py-7 text-[#1b2a4a] sm:px-8 lg:px-14">
      <section className="mb-6">
        <h1 className="font-serif text-3xl font-bold sm:text-4xl">Clientes</h1>
        <p className="mt-2 text-sm text-[#697488]">Pedro Sánchez · Asesoría Sánchez · 1 cliente</p>
      </section>

      <section className="flex flex-col items-start gap-4 rounded-2xl bg-[#1e3057] p-4 text-white shadow-md sm:flex-row sm:items-center sm:p-5">
        <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-[#d96c13] text-2xl font-bold" aria-hidden="true">ϟ</span>
        <div>
          <h2 className="font-semibold">1 comprobante procesado por la IA listo para revisar</h2>
          <p className="mt-1 text-sm text-blue-100">Revisa los gastos extraídos de Ramón en un clic.</p>
        </div>
        <button className="rounded-xl bg-[#d96c13] px-4 py-3 text-sm font-bold text-white transition hover:bg-[#bb5a0c] sm:ml-auto" type="button">Ir a validar bandeja <span className="ml-3" aria-hidden="true">›</span></button>
      </section>

      <section className="my-5 grid gap-3 sm:grid-cols-3" aria-label="Resumen de clientes">
        <article className="rounded-2xl border border-[#dfdbd3] bg-white p-4 shadow-sm">
          <div className="flex items-center justify-between"><span className="size-3 rounded-full bg-red-500" /><strong className="text-2xl">0</strong></div>
          <h3 className="mt-3 text-sm font-semibold">Urgente</h3><p className="mt-1 text-xs text-[#697488]">Requieren acción hoy</p>
        </article>
        <article className="rounded-2xl border border-[#dfdbd3] bg-white p-4 shadow-sm">
          <div className="flex items-center justify-between"><span className="size-3 rounded-full bg-amber-400" /><strong className="text-2xl">1</strong></div>
          <h3 className="mt-3 text-sm font-semibold">Atención</h3><p className="mt-1 text-xs text-[#697488]">Seguimiento necesario</p>
        </article>
        <article className="rounded-2xl border border-[#dfdbd3] bg-white p-4 shadow-sm">
          <div className="flex items-center justify-between"><span className="size-3 rounded-full bg-emerald-500" /><strong className="text-2xl">0</strong></div>
          <h3 className="mt-3 text-sm font-semibold">Al día</h3><p className="mt-1 text-xs text-[#697488]">Sin bloqueos</p>
        </article>
      </section>

      <section className="overflow-hidden rounded-2xl border border-[#dfdbd3] bg-white shadow-sm">
        <header className="flex flex-col justify-between gap-4 border-b border-[#ece9e3] p-4 sm:flex-row sm:items-center sm:px-5">
          <div><h2 className="font-semibold">Clientes · 2T 2026</h2><p className="mt-1 text-xs text-[#697488]">1 cliente</p></div>
          <label className="flex w-full items-center gap-2 rounded-lg border border-[#dfdbd3] bg-[#f5f3ef] px-3 py-2 text-[#7c8592] sm:w-64">
            <span aria-hidden="true">⌕</span><input className="min-w-0 flex-1 bg-transparent text-sm outline-none placeholder:text-[#7c8592]" type="search" placeholder="Buscar cliente o actividad" />
          </label>
        </header>
        <div className="grid gap-4 border-b border-[#ece9e3] p-4 sm:grid-cols-2 sm:items-center sm:px-5 lg:grid-cols-[minmax(220px,2fr)_repeat(4,minmax(85px,1fr))_auto]">
          <div className="flex items-center gap-3 lg:col-auto">
            <span className="size-3 shrink-0 rounded-full bg-amber-400" />
            <div><h3 className="text-sm font-semibold">Ramón García</h3><p className="mt-1 text-xs text-[#697488]">Restauración · E. directa simplificada</p></div>
          </div>
          <div className="text-sm lg:text-center"><p className="text-xs text-[#697488]">Por validar</p><strong className="text-[#c86b1c]">3</strong></div>
          <div className="text-sm lg:text-center"><p className="text-xs text-[#697488]">Faltan por subir</p><strong className="text-[#c86b1c]">4</strong></div>
          <div className="text-sm lg:text-center"><p className="text-xs text-[#697488]">Previsión IVA</p><strong>3.240 €</strong></div>
          <div className="text-sm lg:text-center"><p className="text-xs text-[#697488]">Último contacto</p><strong>ayer</strong></div>
          <button className="rounded-lg bg-[#eeebe4] px-3 py-2 text-sm font-semibold text-[#526078] transition hover:bg-[#e2ded5]" type="button">♧&nbsp; Recordar</button>
        </div>
      </section>
    </div>
  )
}
