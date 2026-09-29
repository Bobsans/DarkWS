import type { Config } from "@docusaurus/types";
import type * as Preset from "@docusaurus/preset-classic";

const repository = "https://github.com/Bobsans/DarkWS";

const config: Config = {
  title: "DarkWS",
  tagline: "Request/response protocol over WebSockets for ASP.NET Core and browsers",
  favicon: "icon.png",
  url: "https://bobsans.github.io",
  baseUrl: "/DarkWS/",
  organizationName: "Bobsans",
  projectName: "DarkWS",
  trailingSlash: false,
  onBrokenLinks: "throw",
  // Generated API reference is CommonMark (.md); MDX is used only for .mdx files.
  markdown: {
    format: "detect",
    hooks: { onBrokenMarkdownLinks: "throw" },
  },
  staticDirectories: ["../assets"],
  i18n: {
    defaultLocale: "en",
    locales: ["en", "ru"],
  },
  presets: [
    [
      "classic",
      {
        docs: {
          routeBasePath: "/",
          // Create a version on a major release: npm run docusaurus docs:version 5.x
          lastVersion: "current",
          versions: {
            current: { label: "5.x" },
            "4.x": { label: "4.x", path: "4.x", banner: "unmaintained" },
          },
          // Generated API pages have no source file to edit.
          editUrl: ({ docPath, locale, version, versionDocsDirPath }) =>
            docPath.startsWith("api/")
              ? undefined
              : locale === "en"
                ? `${repository}/edit/main/website/${versionDocsDirPath}/${docPath}`
                : `${repository}/edit/main/website/i18n/${locale}/docusaurus-plugin-content-docs/${version === "current" ? "current" : `version-${version}`}/${docPath}`,
        },
        blog: false,
      } satisfies Preset.Options,
    ],
  ],
  plugins: [
    [
      "docusaurus-plugin-typedoc",
      {
        entryPoints: ["../packages/darkws/src/index.ts"],
        tsconfig: "../packages/darkws/tsconfig.json",
        out: "docs/api/js",
        fileExtension: ".mdx",
        name: "TypeScript",
        readme: "none",
      },
    ],
  ],
  themeConfig: {
    colorMode: { respectPrefersColorScheme: true },
    navbar: {
      title: "DarkWS",
      logo: { alt: "DarkWS", src: "icon.svg" },
      items: [
        { type: "docsVersionDropdown", position: "right" },
        { type: "localeDropdown", position: "right" },
        { href: repository, label: "GitHub", position: "right" },
      ],
    },
    footer: {
      style: "dark",
      copyright: `MIT License · ${new Date().getFullYear()} Bobsans`,
    },
    prism: { additionalLanguages: ["csharp", "bash", "json"] },
  } satisfies Preset.ThemeConfig,
};

export default config;
