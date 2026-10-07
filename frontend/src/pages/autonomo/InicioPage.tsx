export function InicioPage() {
  return (
    <div className="min-h-[calc(100vh-4rem)] bg-[#f5f3ef] px-6 py-12 text-[#1b2a4a] sm:px-10 lg:px-24">
      <section className="mb-8 max-w-3xl">
        <p className="mb-2 text-xs font-bold uppercase tracking-[0.08em] text-[#c86b1c]">Inicio</p>
        <h1 className="font-serif text-4xl font-bold">Hola, Ramón</h1>
        <p className="mt-2 text-[#647083]">Ten a mano el estado de tu gestión y la ayuda de tu asesoría.</p>
      </section>

      <section className="max-w-3xl rounded-2xl border border-[#dfdbd3] bg-white p-6 shadow-sm" aria-label="Gestor asignado">
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="mb-3 text-xs font-bold uppercase tracking-wide text-[#c86b1c]">Tu gestor asignado</p>
            <div className="flex items-center gap-3">
              <span className="grid size-12 place-items-center rounded-full bg-[#1b2a4a] text-sm font-bold text-white" aria-hidden="true">PS</span>
              <div>
                <h2 className="text-lg font-semibold text-[#1b2a4a]">Pedro Sánchez</h2>
                <p className="mt-1 text-sm text-[#647083]">Asesoría Sánchez</p>
              </div>
            </div>
          </div>
          <span className="whitespace-nowrap rounded-full bg-[#e3f5e9] px-3 py-2 text-xs font-bold text-[#247344]">●&nbsp; Al día</span>
        </div>

        <div className="mt-6 flex flex-col items-start justify-between gap-4 border-t border-[#ece9e3] pt-5 sm:flex-row sm:items-center">
          <p className="text-sm text-[#647083]"><span className="mr-2 inline-grid size-6 place-items-center rounded-full bg-[#fff0df] text-xs font-bold text-[#c86b1c]">3</span>documentos pendientes de su revisión</p>
          <button className="rounded-lg bg-[#1b2a4a] px-4 py-3 text-sm font-bold text-white transition hover:bg-[#2b3e66]" type="button">Enviar mensaje o duda</button>
        </div>
      </section>
    </div>
  )
}
