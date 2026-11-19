# Consignment Operations Console

The React/TypeScript dispatcher console uses the ASP.NET API at `/api`. Start the API
with its HTTP launch profile on `http://localhost:5020`, then run:

```powershell
cd web
npm install
npm run dev
```

Vite proxies `/api` requests to `http://localhost:5020`. Override the proxy target with
`VITE_API_PROXY_TARGET` when the API runs elsewhere.

Run the frontend checks from `web`:

```powershell
npm test
npm run build
npm run lint
```

The tenant selector uses the three demo codes imported from the supplied CSV. Tenant
selection is sent as `X-Tenant` on consignment requests.
