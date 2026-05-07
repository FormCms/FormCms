# FormCMS: The AI-Powered App Platform

FormCMS is an open-source platform that turns natural language into full-stack apps — schemas, APIs, UI, and deployment — in minutes. Ship with Docker, build with AI agents, scale to millions of records.

---

## ✨ Why FormCMS?

<table>
<tr>
<td align="center" width="33%">
<h3>🤖 AI-Powered</h3>
<p>Generate schemas, data, GraphQL queries, and full UI pages from natural language — in the browser or through AI agents.</p>
</td>
<td align="center" width="33%">
<h3>🔌 MCP Server Built-In</h3>
<p>AI agents (Antigravity, Cursor, Codex) connect directly via MCP to design schemas, seed data, and deploy apps — all from the chat window.</p>
</td>
<td align="center" width="33%">
<h3>🚀 Scalable & Performant</h3>
<p>P95 latency under 200ms, 2,400+ QPS throughput. SQLite, PostgreSQL, SQL Server, and MySQL supported.</p>
</td>
</tr>
</table>

---

## 🎥 In Action

Watch FormCMS build a complete Library system (Entities, Data, Queries, and UI) from scratch in under 60 seconds (sped up 10x).

[![FormCMS Demo](https://img.youtube.com/vi/lqjuDNLLaBY/maxresdefault.jpg)](https://www.youtube.com/watch?v=lqjuDNLLaBY)

*Click the image above to watch the full demo on YouTube.*

---

## 🛠️ Three Ways to Build

<table>
<tr>
<td align="center" width="33%">
<h3>🌐 Beginners</h3>
<p><strong>No IDE needed.</strong> Open FormMate in your browser, describe what you want in plain English — AI generates your schemas, sample data, queries, and pages.</p>
<p><a href="https://demo.formcms.com/mate">Try the live demo →</a></p>
</td>
<td align="center" width="33%">
<h3>🎨 Frontend Developers</h3>
<p><strong>Build with AI agents.</strong> Connect Antigravity, Cursor, or Codex to the built-in MCP server — the agent designs your schema, writes your React app, and deploys it.</p>
<p><a href="https://github.com/formcms/formcms/wiki/Vite-React-Antigravity-Example">AI agent setup guide →</a></p>
</td>
<td align="center" width="33%">
<h3>⚙️ Full-Stack Developers</h3>
<p><strong>Extend the platform.</strong> FormMate is Node.js (Fastify), FormCMS is .NET — fork the repo, add custom endpoints, write plugins, or integrate external services.</p>
<p><a href="https://github.com/formcms/formcms/wiki/Setup.md">Dev setup guide →</a></p>
</td>
</tr>
</table>

### 🏗️ Built with FormCMS

| App | Description |
|-----|-------------|
| **[Zen Health Tracker](https://zen.formcms.com/)** | A full health tracking app built in hours using FormCMS + AI agent — zero manual coding. |
| **[Stash PWA](https://demo.formcms.com/stash/)** | A PWA companion app — bookmark, listen (TTS), and cache FormCMS content offline. |

---

## 🚀 Quick Start

> **🟢 Don't want to install?** Try the [live demo](https://demo.formcms.com/mate) instantly — login: `sadmin@cms.com` / `Admin1!`

Pull and run from [Docker Hub](https://hub.docker.com/repository/docker/jaike/formcms-mono):

```bash
docker run -d \
  --name formcms \
  -p 5000:5000 \
  -v formcms_data:/data \
  -e DATABASE_PROVIDER=0 \
  -e "CONNECTION_STRING=Data Source=/data/cms.db" \
  -e FORMCMS_DATA_PATH=/data \
  jaike/formcms-mono:latest
```

| Service | URL |
|---------|-----|
| Admin portal (FormMate) | `http://localhost:5000/mate` |
| REST API | `http://localhost:5000/api/` |
| **MCP server (SSE)** | **`http://localhost:5000/mcp/sse`** |

> **📌 Try these prompts in FormMate:** *"Design entities for a library system"* · *"Add sample data for books"* · *"Create a query to display all books"*
>
> **🤖 Using an AI agent?** Point it at `http://localhost:5000/mcp/sse` to start building via MCP tools.

For production deployment with PostgreSQL, see the [Docker Hub page](https://hub.docker.com/repository/docker/jaike/formcms-mono). Want to contribute or run from source? See the [Development Setup Guide →](https://github.com/formcms/formcms/wiki/Setup.md)

---

## 📚 Learn More

📖 [Documentation Wiki](https://github.com/formcms/formcms/wiki) · [Architecture](https://github.com/formcms/formcms/wiki/Architecture.md) · [Performance & Scalability](https://github.com/formcms/formcms/wiki/Performance-Scalability.md) · [Setup Guide](https://github.com/formcms/formcms/wiki/Setup.md)

---

## 🗺️ Roadmap

FormCMS is actively evolving toward a vision of **AI-native app development**:

| Phase | Focus | Key Features |
|-------|-------|-------------|
| **Enhanced AI** | Smarter generation | Natural language → schema, AI-suggested relationships, auto-generated CRUD & queries |
| **Visual Builder** | No-code editing | Drag-and-drop page builder, visual schema editor, real-time preview, theme templates |
| **Marketplace** | Community ecosystem | Pre-built app templates, community components, one-click install |

> **The Vision:** Describe your app in plain English → AI generates the entire backend → deploy with one click.