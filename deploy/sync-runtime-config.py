#!/usr/bin/python3
"""Merge an allowlisted CI payload into the root-only systemd environment file."""
import json
import os
from pathlib import Path
import re
import tempfile

CONFIG = Path('/etc/anemiascan/api.env')
ALLOWED = {
    'EmailSender__Email', 'EmailSender__Password', 'Smtp__Server', 'Smtp__Port',
    'JwtSettings__Issuer', 'JwtSettings__Audience',
    'JwtSettings__AccessTokenExpirationMinutes', 'JwtSettings__RefreshTokenExpirationDays',
    'CodeGenerator__Length', 'CodeGenerator__Chars', 'CodeGenerator__CacheKey',
    'Legal__PolicyVersion', 'Legal__PolicyUrl',
}


def merge_config(original, payload):
    if not isinstance(payload, dict) or set(payload) - ALLOWED:
        raise ValueError('Unsupported configuration keys')
    updates = {}
    for key, value in payload.items():
        if not isinstance(value, str) or any(ord(c) < 32 for c in value):
            raise ValueError('Invalid configuration value')
        if value:
            updates[key] = value
    for key in ('EmailSender__Email', 'EmailSender__Password', 'Smtp__Server', 'Smtp__Port'):
        if key not in updates:
            raise ValueError('Required SMTP configuration missing')
    if not 1 <= int(updates['Smtp__Port']) <= 65535:
        raise ValueError('Invalid SMTP port')
    lines = [line for line in original.splitlines()
             if not (re.match(r'^([A-Za-z0-9_]+)=', line)
                     and line.split('=', 1)[0] in updates)]
    for key, value in updates.items():
        escaped = value.replace('\\', '\\\\').replace('"', '\\"')
        lines.append(f'{key}="{escaped}"')
    return '\n'.join(lines) + '\n'


def main():
    if os.geteuid() != 0:
        raise SystemExit('Run via the installed sudo helper')
    import sys
    payload = json.loads(sys.stdin.read(65537))
    result = merge_config(CONFIG.read_text(), payload)
    fd, temp_path = tempfile.mkstemp(dir=CONFIG.parent, prefix='.api-env-')
    try:
        with os.fdopen(fd, 'w') as stream:
            stream.write(result)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temp_path, CONFIG)
    finally:
        if os.path.exists(temp_path):
            os.unlink(temp_path)
    print('Runtime configuration updated; existing MongoDB and JWT signing key preserved.')


if __name__ == '__main__':
    main()
