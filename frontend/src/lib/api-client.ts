import axios from "axios"

import { environment } from "@/app/config/environment"

export const apiClient = axios.create({
  baseURL: environment.apiBaseUrl,
  headers: {
    "Content-Type": "application/json",
  },
})
