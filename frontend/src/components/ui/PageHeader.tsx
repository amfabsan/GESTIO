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
        <h1 className="font-serif text-3xl text-[#1d2b4f]">{titulo}</h1>
        {subtitulo && <p className="mt-1 text-[#6b6656]">{subtitulo}</p>}
      </div>
      {derecha}
    </div>
  )
}