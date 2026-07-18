'use client'

import { useState } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import styles from '../dashboard.module.css'

// Opt-in QR image preview. Pure add-on: it renders the stored code string as a scannable
// SVG (client-side, no network call — correct for an opaque token) for printing convenience.
// It does NOT change the stored contract, which stays the code string.
export function QrPreview({ code, testId }: { code: string; testId: string }) {
  const [open, setOpen] = useState(false)
  const trimmed = code.trim()

  return (
    <div data-testid={testId}>
      <button
        className={styles.qrPreviewToggle}
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
      >
        {open ? 'Ocultar vista previa del QR' : 'Mostrar vista previa del QR'}
      </button>
      {open && (
        <div className={styles.qrPreview}>
          {trimmed === '' ? (
            <span className={styles.qrHelp}>Ingresa o genera un código para previsualizar.</span>
          ) : (
            <>
              <QRCodeSVG value={trimmed} size={128} />
              <span className={styles.qrPreviewCode}>{trimmed}</span>
            </>
          )}
        </div>
      )}
    </div>
  )
}
