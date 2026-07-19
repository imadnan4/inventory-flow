import { create } from "zustand"

import type {
  AuthenticationResponse,
  AuthenticatedUser,
} from "@/features/auth/types"
import { queryClient } from "@/lib/query-client"

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
    set((state) => {
      if (
        state.user &&
        (state.user.id !== session.user.id ||
          state.user.workspace.id !== session.user.workspace.id)
      )
        queryClient.clear()

      return { accessToken: session.accessToken, user: session.user }
    }),
  clearSession: () => {
    queryClient.clear()
    set({ accessToken: null, user: null })
  },
  finishRestoring: () => set({ isRestoring: false }),
}))
