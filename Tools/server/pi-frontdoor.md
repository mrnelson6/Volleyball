# Pi front door for volleyball.ttnelson.com

The Raspberry Pi stays the single internet-facing entry point, exactly like the
other sites: it owns DNS + TLS and reverse-proxies to the game box (marvin,
`192.168.0.240:8090`), which serves the WebGL build and the spawn endpoint over
plain LAN HTTP.

## 1. DNS
At the `ttnelson.com` DNS provider add one record:

    CNAME  volleyball  ->  ttnelson.com
    (or an A record to the same public IP)

## 2. Pi reverse proxy — use whichever the Pi runs

**Caddy** (`/etc/caddy/Caddyfile`) — TLS is automatic once DNS resolves:

    volleyball.ttnelson.com {
        reverse_proxy 192.168.0.240:8090
    }

**nginx** (`/etc/nginx/sites-available/volleyball.ttnelson.com`, then symlink
into `sites-enabled` and reload). Get the cert the same way as the other sites
(`sudo certbot --nginx -d volleyball.ttnelson.com`):

    server {
        server_name volleyball.ttnelson.com;
        listen 80;
        # certbot will add the 443/TLS mirror of this block

        location / {
            proxy_pass http://192.168.0.240:8090;
            proxy_set_header Host $host;
            proxy_read_timeout 60s;   # /spawn can take a few seconds
        }
    }

No other changes: ports 80/443 already reach the Pi, and actual game traffic
never touches either box — clients (browser included) talk outbound to Unity
Relay.
