import LoginCard from './LoginCard'
import { buildResetCredentialsUrl } from '@/app/lib/keycloak'

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ error?: string }>
}) {
  const params = await searchParams
  const error = params.error

  return <LoginCard error={error} forgotPasswordHref={buildResetCredentialsUrl()} />
}
