import { createContext, useContext, useEffect, useState } from "react"
import {useAuth} from "@/entities/session";

type Theme = "dark" | "light" | "win69"

type ThemeProviderProps = {
    children: React.ReactNode
    defaultTheme?: Theme
    storageKey?: string
}

type ThemeProviderState = {
    theme: Theme
    setTheme: (theme: Theme) => void
}

const initialState: ThemeProviderState = {
    theme: "dark",
    setTheme: () => null,
}

const ThemeProviderContext = createContext<ThemeProviderState>(initialState)

export function ThemeProvider({
                                  children,
                                  defaultTheme = "dark",
                                  storageKey = "vite-ui-theme",
                                  ...props
                              }: ThemeProviderProps) {
    const [theme, setTheme] = useState<Theme>(
        () => (localStorage.getItem(storageKey) as Theme) || defaultTheme
    )

    const auth = useAuth();

    useEffect(() => {
        if (auth.status === "authenticated" && auth.user?.theme) {
            setTheme(auth.user.theme as Theme);
            localStorage.setItem(storageKey, auth.user.theme);
        } else if (auth.status === "guest") {
            setTheme(defaultTheme);
            localStorage.setItem(storageKey, defaultTheme);
        }
    }, [auth.status, auth.user?.theme, defaultTheme, storageKey]);

    useEffect(() => {
        const root = window.document.documentElement
        root.classList.remove("light", "dark", "win69")
        root.classList.add(theme)
    }, [theme])

    const value = {
        theme,
        setTheme: (theme: Theme) => {
            localStorage.setItem(storageKey, theme)
            setTheme(theme)
        },
    }

    return (
        <ThemeProviderContext.Provider {...props} value={value}>
            {children}
        </ThemeProviderContext.Provider>
    )
}

export const useTheme = () => {
    const context = useContext(ThemeProviderContext)
    if (context === undefined)
        throw new Error("useTheme must be used within a ThemeProvider")
    return context
}