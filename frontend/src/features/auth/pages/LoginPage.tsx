import { useState } from "react"
import { Link, useLocation, useNavigate } from "react-router"
import { loginSchema } from "@/features/auth/auth-schema"
import { login } from "@/features/auth/auth-api"
import { useAuthStore } from "@/features/auth/auth-store"
import { getSafeReturnPath } from "@/features/auth/auth-redirect"
export function Component() {
  const navigate = useNavigate()
  const location = useLocation()
  const setSession = useAuthStore((state) => state.setSession)
  const [error, setError] = useState("")
  const [invalidField, setInvalidField] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const submit = async (form: FormData) => {
    const parsed = loginSchema.safeParse(Object.fromEntries(form))
    if (!parsed.success) {
      setInvalidField(parsed.error.issues[0]?.path[0]?.toString() ?? null)
      return setError(parsed.error.issues[0]?.message ?? "Check your details.")
    }

    setPending(true)
    setError("")
    setInvalidField(null)
    try {
      setSession(await login(parsed.data))
      navigate(getSafeReturnPath(location.state?.from), { replace: true })
    } catch {
      setError("Email or password is incorrect.")
    } finally {
      setPending(false)
    }
  }
  return (
    <main className="grid min-h-svh place-items-center p-6">
      <form
        action={submit}
        className="w-full max-w-sm space-y-4 rounded-xl border bg-card p-6 shadow-sm"
      >
        <div>
          <h1 className="text-2xl font-semibold">Sign in</h1>
          <p className="text-sm text-muted-foreground">
            Access your inventory workspace.
          </p>
        </div>
        <label className="block text-sm">
          Email
          <input
            className="mt-1 w-full rounded-md border bg-background p-2"
            name="email"
            type="email"
            aria-describedby={
              invalidField === "email" ? "login-error" : undefined
            }
            aria-invalid={invalidField === "email"}
            autoComplete="email"
          />
        </label>
        <label className="block text-sm">
          Password
          <input
            className="mt-1 w-full rounded-md border bg-background p-2"
            name="password"
            type="password"
            aria-describedby={
              invalidField === "password" ? "login-error" : undefined
            }
            aria-invalid={invalidField === "password"}
            autoComplete="current-password"
          />
        </label>
        {error && (
          <p
            aria-live="assertive"
            className="text-sm text-destructive"
            id="login-error"
            role="alert"
          >
            {error}
          </p>
        )}
        <button
          className="w-full rounded-md bg-primary p-2 text-primary-foreground disabled:opacity-50"
          disabled={pending}
        >
          {pending ? "Signing in…" : "Sign in"}
        </button>
        <p className="text-sm text-muted-foreground">
          New here?{" "}
          <Link
            className="text-primary underline"
            state={location.state}
            to="/register"
          >
            Create an account
          </Link>
        </p>
      </form>
    </main>
  )
}
