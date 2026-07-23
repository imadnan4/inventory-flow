import {
  Alert02Icon,
  MoneyReceiveSquareIcon,
  PackageIcon,
  ShoppingCart01Icon,
  WarehouseIcon,
} from "@hugeicons/core-free-icons"
import type { IconSvgElement } from "@hugeicons/react"
import { HugeiconsIcon } from "@hugeicons/react"
import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts"

import { MetricCard } from "@/features/dashboard/components/MetricCard"
import { PageHeader } from "@/components/shared/PageHeader"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"

type Metric = {
  label: string
  value: string
  change: string
  trend: "up" | "down"
  icon: IconSvgElement
}

const metrics: Metric[] = [
  {
    label: "Total products",
    value: "1,248",
    change: "12.5%",
    trend: "up",
    icon: PackageIcon,
  },
  {
    label: "Inventory value",
    value: "$84,250",
    change: "8.2%",
    trend: "up",
    icon: MoneyReceiveSquareIcon,
  },
  {
    label: "Today's sales",
    value: "$3,480",
    change: "4.1%",
    trend: "up",
    icon: ShoppingCart01Icon,
  },
  {
    label: "Low stock items",
    value: "24",
    change: "2.4%",
    trend: "down",
    favorable: true,
    icon: Alert02Icon,
  },
]

const salesData = [
  { month: "Jan", revenue: 18_400 },
  { month: "Feb", revenue: 21_200 },
  { month: "Mar", revenue: 19_600 },
  { month: "Apr", revenue: 24_800 },
  { month: "May", revenue: 28_100 },
  { month: "Jun", revenue: 31_500 },
]

const recentActivity = [
  {
    title: "Purchase order PO-1048 received",
    detail: "Northwind Traders · 24 items",
    time: "12 min ago",
  },
  {
    title: "Stock adjusted for Wireless Barcode Scanner",
    detail: "Main Warehouse · -2 units",
    time: "38 min ago",
  },
  {
    title: "Sales order SO-3012 dispatched",
    detail: "Acme Retail · 8 items",
    time: "1 hr ago",
  },
]

export function Component() {
  return (
    <div className="space-y-8">
      <PageHeader
        actions={<Button disabled>Export report</Button>}
        description="A real-time view of inventory health and operational activity."
        title="Dashboard"
      />

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {metrics.map((metric, index) => (
          <MetricCard {...metric} index={index} key={metric.label} />
        ))}
      </section>

      <section className="grid gap-6 xl:grid-cols-[1.7fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Revenue overview</CardTitle>
            <CardDescription>
              Monthly sales performance for the current year.
            </CardDescription>
          </CardHeader>
          <CardContent className="h-72">
            <ResponsiveContainer height="100%" width="100%">
              <AreaChart
                data={salesData}
                margin={{ bottom: 0, left: -20, right: 8, top: 8 }}
              >
                <defs>
                  <linearGradient id="revenue" x1="0" x2="0" y1="0" y2="1">
                    <stop
                      offset="5%"
                      stopColor="var(--primary)"
                      stopOpacity={0.35}
                    />
                    <stop
                      offset="95%"
                      stopColor="var(--primary)"
                      stopOpacity={0}
                    />
                  </linearGradient>
                </defs>
                <CartesianGrid
                  stroke="var(--border)"
                  strokeDasharray="3 3"
                  vertical={false}
                />
                <XAxis
                  axisLine={false}
                  dataKey="month"
                  tickLine={false}
                  tickMargin={12}
                />
                <YAxis
                  axisLine={false}
                  tickFormatter={(value: number) => `$${value / 1_000}k`}
                  tickLine={false}
                  tickMargin={8}
                />
                <Tooltip
                  contentStyle={{
                    backgroundColor: "var(--popover)",
                    border: "1px solid var(--border)",
                    borderRadius: "var(--radius)",
                  }}
                  formatter={(value) => [
                    `$${Number(value).toLocaleString()}`,
                    "Revenue",
                  ]}
                />
                <Area
                  dataKey="revenue"
                  fill="url(#revenue)"
                  stroke="var(--primary)"
                  strokeWidth={2}
                  type="monotone"
                />
              </AreaChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Warehouse coverage</CardTitle>
            <CardDescription>
              Stock distribution across locations.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            {[
              ["Main Warehouse", "62%", "782 items"],
              ["East Distribution", "24%", "299 items"],
              ["West Distribution", "14%", "167 items"],
            ].map(([name, percentage, count]) => (
              <div key={name}>
                <div className="flex items-center justify-between text-sm">
                  <span className="font-medium">{name}</span>
                  <span className="text-muted-foreground">{count}</span>
                </div>
                <div className="mt-2 h-2 overflow-hidden rounded-full bg-muted">
                  <div
                    className="h-full rounded-full bg-primary"
                    style={{ width: percentage }}
                  />
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-6 xl:grid-cols-[1.7fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Recent activity</CardTitle>
            <CardDescription>
              Latest movements across your operations.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {recentActivity.map((activity) => (
              <div className="flex items-start gap-3" key={activity.title}>
                <div className="mt-0.5 grid size-8 shrink-0 place-items-center rounded-full bg-muted text-muted-foreground">
                  <HugeiconsIcon icon={PackageIcon} size={16} />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium">{activity.title}</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {activity.detail}
                  </p>
                </div>
                <span className="text-xs whitespace-nowrap text-muted-foreground">
                  {activity.time}
                </span>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Inventory alerts</CardTitle>
            <CardDescription>Items that need attention.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex items-center justify-between gap-3 rounded-lg bg-destructive/10 p-3">
              <div className="flex items-center gap-2">
                <HugeiconsIcon
                  className="text-destructive"
                  icon={Alert02Icon}
                  size={18}
                />
                <span className="text-sm font-medium">24 low stock items</span>
              </div>
              <Badge variant="outline">Review</Badge>
            </div>
            <div className="flex items-center justify-between gap-3 rounded-lg bg-muted p-3">
              <div className="flex items-center gap-2">
                <HugeiconsIcon icon={WarehouseIcon} size={18} />
                <span className="text-sm font-medium">3 pending transfers</span>
              </div>
              <Badge variant="secondary">Open</Badge>
            </div>
          </CardContent>
        </Card>
      </section>
    </div>
  )
}
