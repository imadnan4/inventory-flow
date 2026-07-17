import { useState } from "react"
import {
  Menu01Icon,
  Moon02Icon,
  Notification01Icon,
  Search01Icon,
  Sun03Icon,
} from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { NavigationLinks } from "@/components/layout/Sidebar"
import { useTheme } from "@/components/theme-provider"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  Sheet,
  SheetContent,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet"
import { useUiStore } from "@/store/ui-store"

export function Topbar() {
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false)
  const { setTheme, theme } = useTheme()
  const toggleSidebar = useUiStore((state) => state.toggleSidebar)

  const toggleTheme = () => {
    setTheme(theme === "dark" ? "light" : "dark")
  }

  return (
    <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b bg-background/95 px-4 backdrop-blur sm:px-6 lg:px-8">
      <Sheet onOpenChange={setIsMobileNavOpen} open={isMobileNavOpen}>
        <SheetTrigger
          render={<Button className="lg:hidden" size="icon" variant="ghost" />}
        >
          <HugeiconsIcon icon={Menu01Icon} size={20} />
          <span className="sr-only">Open navigation</span>
        </SheetTrigger>
        <SheetContent className="w-72 p-3" side="left">
          <SheetTitle className="sr-only">Main navigation</SheetTitle>
          <div className="mb-6 flex h-10 items-center gap-3 px-2">
            <div className="size-7 rounded-md bg-primary" />
            <span className="font-semibold">Inventory Flow</span>
          </div>
          <NavigationLinks onNavigate={() => setIsMobileNavOpen(false)} />
        </SheetContent>
      </Sheet>

      <Button
        className="hidden lg:inline-flex"
        onClick={toggleSidebar}
        size="icon"
        variant="ghost"
      >
        <HugeiconsIcon icon={Menu01Icon} size={20} />
        <span className="sr-only">Toggle sidebar</span>
      </Button>

      <div className="ml-auto flex items-center gap-1 sm:gap-2">
        <Button size="icon" variant="ghost">
          <HugeiconsIcon icon={Search01Icon} size={20} />
          <span className="sr-only">Search</span>
        </Button>
        <Button onClick={toggleTheme} size="icon" variant="ghost">
          <HugeiconsIcon
            icon={theme === "dark" ? Sun03Icon : Moon02Icon}
            size={20}
          />
          <span className="sr-only">Toggle theme</span>
        </Button>
        <Button size="icon" variant="ghost">
          <HugeiconsIcon icon={Notification01Icon} size={20} />
          <span className="sr-only">Notifications</span>
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={<Button className="ml-1" size="icon" variant="ghost" />}
          >
            <Avatar className="size-7">
              <AvatarFallback>AA</AvatarFallback>
            </Avatar>
            <span className="sr-only">Open user menu</span>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-48">
            <DropdownMenuLabel>Adnan Ahmad</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem>Profile</DropdownMenuItem>
            <DropdownMenuItem>Settings</DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem variant="destructive">Sign out</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  )
}
