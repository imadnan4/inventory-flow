import {
  ArrowDownRightIcon,
  ArrowUpRightIcon,
} from "@hugeicons/core-free-icons"
import { HugeiconsIcon, type IconSvgElement } from "@hugeicons/react"
import { motion } from "motion/react"

import { Card, CardContent } from "@/components/ui/card"
import { cn } from "@/lib/utils"

type MetricCardProps = {
  label: string
  value: string
  change: string
  trend: "up" | "down"
  favorable?: boolean
  icon: IconSvgElement
  index: number
}

export function MetricCard({
  label,
  value,
  change,
  trend,
  favorable = trend === "up",
  icon,
  index,
}: MetricCardProps) {
  const isPositive = favorable

  return (
    <motion.div
      animate={{ opacity: 1, y: 0 }}
      initial={{ opacity: 0, y: 12 }}
      transition={{ delay: index * 0.06, duration: 0.25 }}
    >
      <Card className="h-full">
        <CardContent className="flex items-start justify-between">
          <div>
            <p className="text-sm text-muted-foreground">{label}</p>
            <p className="mt-2 text-2xl font-semibold tracking-tight">
              {value}
            </p>
            <p
              className={cn(
                "mt-2 flex items-center gap-1 text-xs font-medium",
                isPositive
                  ? "text-emerald-600 dark:text-emerald-400"
                  : "text-destructive"
              )}
            >
              <HugeiconsIcon
                icon={isPositive ? ArrowUpRightIcon : ArrowDownRightIcon}
                size={14}
                strokeWidth={2}
              />
              {change} from last month
            </p>
          </div>
          <div className="grid size-10 place-items-center rounded-lg bg-muted text-muted-foreground">
            <HugeiconsIcon icon={icon} size={20} strokeWidth={1.8} />
          </div>
        </CardContent>
      </Card>
    </motion.div>
  )
}
