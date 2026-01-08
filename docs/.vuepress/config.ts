import { defineUserConfig } from 'vuepress'
import { defaultTheme } from '@vuepress/theme-default'
import { viteBundler } from '@vuepress/bundler-vite'
import { searchPlugin } from '@vuepress/plugin-search'

export default defineUserConfig({
  lang: 'zh-CN',
  title: 'Croupier C# SDK',
  description: 'Croupier SDK for .NET 8+',
  head: [
    ['meta', { name: 'viewport', content: 'width=device-width,initial-scale=1' }],
    ['meta', { name: 'keywords', content: 'croupier,csharp,.net,sdk,grpc' }],
    ['meta', { name: 'theme-color', content: '#3eaf7c' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:locale', content: 'zh-CN' }],
    ['meta', { property: 'og:title', content: 'Croupier C# SDK' }],
    ['meta', { property: 'og:site_name', content: 'Croupier' }],
  ],
  base: '/croupier-sdk-csharp/',
  bundler: viteBundler(),
  theme: defaultTheme({
    repo: 'cuihairu/croupier-sdk-csharp',
    repoLabel: 'GitHub',
    docsDir: 'docs',
    docsBranch: 'main',
    editLinkText: '在 GitHub 上编辑此页',
    lastUpdated: true,
    lastUpdatedText: '最后更新',
    contributors: false,

    navbar: [
      {
        text: '指南',
        link: '/guide/',
      },
      {
        text: 'API 参考',
        link: '/api/',
      },
    ],

    sidebar: {
      '/guide/': [
        {
          text: '开始使用',
          collapsable: false,
          children: [
            '/guide/README.md',
            '/guide/installation.md',
            '/guide/quick-start.md',
            '/guide/configuration.md',
            '/guide/dependency-injection.md',
          ],
        },
        {
          text: '高级用法',
          children: [
            '/guide/async-handlers.md',
            '/guide/error-handling.md',
            '/guide/unity-integration.md',
          ],
        },
      ],

      '/api/': [
        {
          text: 'API 参考',
          collapsible: false,
          children: [
            '/api/README.md',
            '/api/client.md',
            '/api/invoker.md',
            '/api/models.md',
          ],
        },
      ],

      '/': [
        {
          text: '指南',
          collapsible: false,
          children: [
            '/guide/README.md',
            '/guide/quick-start.md',
          ],
        },
        {
          text: 'API',
          children: [
            '/api/README.md',
          ],
        },
      ],
    },
  }),

  plugins: [
    searchPlugin({
      locales: {
        '/': {
          placeholder: '搜索文档',
        },
      },
      maxSuggestions: 10,
    }),
  ],
})
