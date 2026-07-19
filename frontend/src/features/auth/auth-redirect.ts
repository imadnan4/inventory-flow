export const getSafeReturnPath = (value: unknown) => {
  if (
    typeof value !== "string" ||
    !value.startsWith("/") ||
    value.startsWith("//")
  )
    return "/dashboard"

  try {
    const url = new URL(value, window.location.origin)
    return url.origin === window.location.origin
      ? `${url.pathname}${url.search}${url.hash}`
      : "/dashboard"
  } catch {
    return "/dashboard"
  }
}
