import { create } from "zustand"
import type {
  AuthenticationResponse,
  AuthenticatedUser,
} from "@/features/auth/types"

type AuthState = {
  accessToken: string | null
  user: AuthenticatedUser | null
  isRestoring: boolean
  setSession: (session: AuthenticationResponse) => void
  clearSession: () => void
  finishRestoring: () => void
}
export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  user: null,
  isRestoring: true,
  setSession: (session) =>
    set({ accessToken: session.accessToken, user: session.user }),
  clearSession: () => set({ accessToken: null, user: null }),
  finishRestoring: () => set({ isRestoring: false }),
}))
