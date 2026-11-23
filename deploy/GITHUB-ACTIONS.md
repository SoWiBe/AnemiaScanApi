# New server deployment

The single workflow `deployapi.yml` uses the GitHub environment `Master`.
It is manually triggered and restricted to the `master` branch during migration.
It replaces the former deployment that overwrote configuration from GitHub secrets.
Do not change DNS until the new API and HTTPS have been checked.

## Server preparation

Run the uploaded script interactively:

```bash
sudo bash ~/setup-actions.sh root1
```

This requires completed database provisioning and an empty application directory.
It creates release directories and grants the SSH user permission to run commands
as the application user and restart only `anemiascan-api` as root. It does not
grant general root sudo access. It also installs the root-owned configuration
helper from `sync-runtime-config.py` alongside the setup script. The helper accepts
only approved runtime keys through stdin and atomically updates the root-only
`/etc/anemiascan/api.env`; no credentials are printed or placed in the artifact.

Before deployment, CI syncs `EMAIL_SENDER`, `EMAIL_PASSWORD`, `SMTP_SERVER`,
`SMTP_PORT`, JWT issuer/audience/lifetimes, code-generator and legal settings from
`Master`. All four SMTP secrets are required; empty optional secrets preserve the
server value. MongoDB credentials and the JWT signing key remain server-managed
to avoid restoring obsolete values from the previous VPS. `JWT_KEY` and collection
name secrets are not used. A build-only run does not update server configuration.
Never paste configuration contents into logs or issues. Config changes are not
automatically reverted if application readiness later fails.

## GitHub environment

Use **Settings → Environments → Master**. Set these environment secrets:

| Name | Value |
|---|---|
| `VPS_HOST` | New server address |
| `VPS_USERNAME` | SSH deployment username |
| `VPS_SSH_KEY` | Dedicated CI private key, without a passphrase |
| `VPS_KNOWN_HOSTS` | Verified OpenSSH known_hosts entry for the server |
| `SIXLABORS_LICENSE_KEY` | Valid build license key |

Optional environment variable: `VPS_PORT` (defaults to `22`). Add only the CI
public key to the deployment user's `authorized_keys`. Do not reuse the personal
encrypted key. Verify the host fingerprint through the existing trusted session
before storing its known_hosts entry; the workflow never trusts ssh-keyscan blindly.

## Release

After committing and pushing the reviewed changes, run **Actions → Deploy
AnemiaScanApi → Run workflow**, selecting `master`. Tests run in Debug, then publishing
uses Release and the license key. Mongo-backed tests still require their separate
test database configuration; production credentials are not supplied to tests.

If direct GitHub-to-VPS transfer is slow, select `build_only` when dispatching.
The job saves `api.tar.gz` and its SHA-256 checksum as a seven-day Actions artifact,
without connecting to the server or deploying. Download that artifact through a
trusted workstation and relay the unchanged archive over SSH. Verify its checksum
on the server before extraction. A successful build-only job is not a deployment.
Normal runs also save the artifact before transfer; upload has a ten-minute limit.

Artifacts are extracted as the application user into separate release directories.
The workflow switches the current symlink, restarts the service and checks
`/health/ready`. If readiness times out, it restores the prior symlink when one
exists. First deployment has no previous release to restore. Old releases remain
on disk; monitor space and retain needed releases deliberately.

Readiness checks do not exercise actual inference or SMTP. Verify these separately
before switching DNS. The current workflow does not roll back database changes
or course imports, and an SSH interruption can require manual recovery.
