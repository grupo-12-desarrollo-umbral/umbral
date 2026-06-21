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
        {open ? 'Hide QR preview' : 'Show QR preview'}
      </button>
      {open && (
        <div className={styles.qrPreview}>
          {trimmed === '' ? (
            <span className={styles.qrHelp}>Enter or generate a code to preview.</span>
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
