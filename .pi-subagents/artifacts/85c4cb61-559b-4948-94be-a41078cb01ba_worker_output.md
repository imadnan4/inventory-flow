Implemented non-root frontend Nginx image with unprivileged port 8080 and Compose port mapping.

Changed files: `frontend/Dockerfile`, `frontend/nginx.conf`, `docker-compose.yml`.

Validation: Compose build and full proxy smoke passed; runtime UID is 101; SPA root/fallback return 200 and API proxy returns 401.

Open risks/questions: none.

Recommended next step: reviewer acceptance.