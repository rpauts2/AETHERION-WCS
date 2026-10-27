# SQLite operations for AETHERION WCS

This guide covers safe backups and recovery for the current SQLite player store.

## Before a release

1. Stop the CS2 server cleanly, or put the server into maintenance mode so no new player/economy writes can occur.
2. Record the plugin version and the database file path from the server configuration.
3. Create a dated backup outside the live database directory.
4. Keep at least one recent backup on a different disk or host.
5. Test restoration against a copy of the database before upgrading a production server.

## Backup options

### Preferred: SQLite online backup

If the server can remain online, use SQLite's online backup API or a trusted SQLite backup utility. This produces a consistent snapshot while SQLite manages active transactions.

With the `sqlite3` CLI, open the live database and run:

```sql
.backup 'AetherionWcs-backup-YYYYMMDD-HHMMSS.db'
```

Replace the placeholder with a real timestamp. Do not run the literal placeholder command.

### Simple maintenance-window backup

Stop the CS2 server cleanly first, then copy the database file to a dated backup location. When using WAL mode, stopping cleanly allows SQLite to checkpoint and close the database safely. Do not copy only the main `.db` file while the database is actively being written: recent committed data may still be in the `-wal` file.

## Restore

1. Stop the CS2 server.
2. Move the current database aside rather than overwriting the only copy.
3. Restore the selected backup to the exact configured database path.
4. Start the server and inspect plugin logs for SQLite errors and profile-load failures.
5. Verify a test account's race, level, currency, contracts, and guild state before reopening the server.

Never test a restore by overwriting the only production database.

## Current implementation notes

- The store uses SQLite WAL mode to reduce reader/writer blocking.
- The connection string sets a bounded 5-second default command timeout for transient database contention.
- The `crafted_ids` schema upgrade checks `PRAGMA table_info` before altering the table. Unexpected migration errors are no longer silently swallowed.
- This is not yet a complete versioned migration framework. Future schema changes should have explicit version numbers, transactional migration steps where supported, and tests against copies of older database schemas.
- A CI build does not prove that an existing production database upgrades correctly. Test upgrades with a copy of the actual database before deploying.