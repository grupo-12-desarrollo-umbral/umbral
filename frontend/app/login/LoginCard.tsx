'use client'

import styles from './login.module.css'

export default function LoginCard({
  error,
  forgotPasswordHref,
}: {
  error?: string
  forgotPasswordHref: string
}) {
  return (
    <div className={styles.page}>
      <div className={styles.card}>
        <div className={styles.brandMark} aria-hidden="true">
          <CompassMark />
        </div>
        <h1 className={styles.title}>Umbral</h1>
        <p className={styles.subtitle}>Command center</p>

        {error === 'deactivated' && (
          <div className={styles.errorChip} data-testid="deactivated-chip">
            Your account has been deactivated.
          </div>
        )}

        {error === 'unauthorized' && (
          <div className={styles.errorChip}>
            Authentication failed. Try again.
          </div>
        )}

        <a className={styles.primaryButton} href="/api/auth/login">
          Sign in with Keycloak
        </a>

        <a className={styles.secondaryLink} href={forgotPasswordHref}>
          Forgot your password?
        </a>
      </div>
    </div>
  )
}

function CompassMark() {
  return (
    <svg aria-hidden="true" height="34" viewBox="0 0 34 34" width="34">
      <circle cx="17" cy="17" fill="none" r="15.5" stroke="currentColor" strokeWidth="1.2" />
      <circle cx="17" cy="17" fill="none" opacity="0.35" r="10.4" stroke="currentColor" strokeWidth="1" />
      <path
        d="M17 4.5 19.8 14.2 29.5 17 19.8 19.8 17 29.5 14.2 19.8 4.5 17 14.2 14.2Z"
        fill="none"
        stroke="currentColor"
        strokeLinejoin="round"
        strokeWidth="1.15"
      />
      <path d="M17 7.5V26.5M7.5 17H26.5" opacity="0.42" stroke="currentColor" strokeLinecap="round" strokeWidth="1" />
    </svg>
  )
}
