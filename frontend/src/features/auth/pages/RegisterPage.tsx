import { useState } from "react"
import { Link, useLocation, useNavigate } from "react-router"
import { registerSchema } from "@/features/auth/auth-schema"
import { register } from "@/features/auth/auth-api"
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
    const parsed = registerSchema.safeParse(Object.fromEntries(form))
    if (!parsed.success) {
      setInvalidField(parsed.error.issues[0]?.path[0]?.toString() ?? null)
      return setError(parsed.error.issues[0]?.message ?? "Check your details.")
    }

    setPending(true)
    setError("")
    setInvalidField(null)
    try {
      setSession(await register(parsed.data))
      navigate(getSafeReturnPath(location.state?.from), { replace: true })
    } catch {
      setError(
        "We could not create your account. Check the password requirements and try again."
      )
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
          <h1 className="text-2xl font-semibold">Create account</h1>
          <p className="text-sm text-muted-foreground">
            Start managing inventory.
          </p>
        </div>
        <label className="block text-sm">
          Display name
          <input
            className="mt-1 w-full rounded-md border bg-background p-2"
            name="displayName"
            aria-describedby={
              invalidField === "displayName" ? "register-error" : undefined
            }
            aria-invalid={invalidField === "displayName"}
            autoComplete="name"
          />
        </label>
        <label className="block text-sm">
          Email
          <input
            className="mt-1 w-full rounded-md border bg-background p-2"
            name="email"
            type="email"
            aria-describedby={
              invalidField === "email" ? "register-error" : undefined
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
              invalidField === "password" ? "register-error" : undefined
            }
            aria-invalid={invalidField === "password"}
            autoComplete="new-password"
          />
        </label>
        {error && (
          <p
            aria-live="assertive"
            className="text-sm text-destructive"
            id="register-error"
            role="alert"
          >
            {error}
          </p>
        )}
        <button
          className="w-full rounded-md bg-primary p-2 text-primary-foreground disabled:opacity-50"
          disabled={pending}
        >
          {pending ? "Creating…" : "Create account"}
        </button>
        <p className="text-sm text-muted-foreground">
          Already registered?{" "}
          <Link
            className="text-primary underline"
            state={location.state}
            to="/login"
          >
            Sign in
          </Link>
        </p>
      </form>
    </main>
  )
}
