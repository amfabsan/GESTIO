import type { ReactNode } from 'react'

type PageHeaderProps = {
  titulo: string
  subtitulo?: ReactNode
  derecha?: ReactNode
}

export function PageHeader({ titulo, subtitulo, derecha }: PageHeaderProps) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 className="text-[34px] font-bold leading-tight text-tinta">{titulo}</h1>
        {subtitulo && <p className="mt-1 text-base text-texto-gris">{subtitulo}</p>}
      </div>
      {derecha}
    </div>
  )
}