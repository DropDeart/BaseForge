import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { en, type Messages } from "./en";
import { tr } from "./tr";

// Arayüz dili: kullanıcının seçimi localStorage'da; seçim yoksa tarayıcı dili Türkçe ise tr, değilse en.

export type Lang = "en" | "tr";

export const LANGS: { code: Lang; label: string }[] = [
  { code: "en", label: "EN" },
  { code: "tr", label: "TR" },
];

const MESSAGES: Record<Lang, Messages> = { en, tr };
const STORAGE_KEY = "baseforge.designer.lang";

function initialLang(): Lang {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === "en" || stored === "tr") return stored;
  } catch {
    // localStorage kapalı olabilir (gizli pencere vb.) — tarayıcı diline düş.
  }
  return navigator.language?.toLowerCase().startsWith("tr") ? "tr" : "en";
}

const I18nContext = createContext<{ lang: Lang; setLang: (lang: Lang) => void; m: Messages } | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(initialLang);

  useEffect(() => {
    document.documentElement.lang = lang;
  }, [lang]);

  const setLang = (next: Lang) => {
    setLangState(next);
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Kalıcı olmasa da oturum boyunca geçerli.
    }
  };

  return <I18nContext.Provider value={{ lang, setLang, m: MESSAGES[lang] }}>{children}</I18nContext.Provider>;
}

function useI18n() {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("I18nProvider is missing.");
  return ctx;
}

/** O anki dilin metinleri. */
export function useT(): Messages {
  return useI18n().m;
}

/** O anki dil ve değiştirici. */
export function useLang(): { lang: Lang; setLang: (lang: Lang) => void } {
  const { lang, setLang } = useI18n();
  return { lang, setLang };
}

export type { Messages };
