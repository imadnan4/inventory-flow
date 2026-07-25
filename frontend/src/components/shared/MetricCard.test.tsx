import { describe, it, expect } from "vitest"
import { render, screen } from "@testing-library/react"
import { MetricCard } from "./MetricCard"

describe("MetricCard", () => {
  it("renders the metric title", () => {
    render(<MetricCard title="Total Products" value={42} />)
    expect(screen.getByText("Total Products")).toBeInTheDocument()
    expect(screen.getByText("42")).toBeInTheDocument()
  })

  it("renders a favorable indicator when provided", () => {
    render(<MetricCard title="Stock Level" value={15} favorable={true} />)
    expect(screen.getByText("Stock Level")).toBeInTheDocument()
  })

  it("renders an unfavorable indicator when provided", () => {
    render(<MetricCard title="Low Stock Items" value={3} favorable={false} />)
    expect(screen.getByText("Low Stock Items")).toBeInTheDocument()
    expect(screen.getByText("-3")).toBeInTheDocument()
  })
})