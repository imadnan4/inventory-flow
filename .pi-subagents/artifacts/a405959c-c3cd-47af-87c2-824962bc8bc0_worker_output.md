Implemented non-root API and EF migration Docker stages with owned writable paths.

Changed files: `backend/Dockerfile`.

Validation: rebuilt API/migrator images; Compose migration completed; API `/health` returned `Healthy`; both images run as UID/GID 10001.

Open risks/questions: Independent reviewer gate remains required.

Recommended next step: Perform required acceptance review.