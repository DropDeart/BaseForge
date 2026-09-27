import { h } from "vue";
import DefaultTheme from "vitepress/theme";
import type { Theme } from "vitepress";
import HomeSections from "./components/HomeSections.vue";
import "./custom.css";

export default {
  extends: DefaultTheme,
  Layout() {
    return h(DefaultTheme.Layout, null, {
      // Extra landing-page sections below the feature grid (only rendered on `layout: home` pages).
      "home-features-after": () => h(HomeSections),
    });
  },
} satisfies Theme;
