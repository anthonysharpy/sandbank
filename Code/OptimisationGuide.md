# Optimising Performance

The default settings will be very fast for 99% of people. That being said, here are some ways you might be able to squeeze even more performance out of the database:

- Use `Saved` instead of `AutoSaved`.
- Don't save your records every time they are changed. Instead, have a background loop that saves every record every second or so.
- Disable file obfuscation.
- Increase `PERSIST_EVERY_N_SECONDS`.
- Increase `CLASS_INSTANCE_POOL_SIZE`.
- Use the `UnsafeReferences` methods.
- Disable `INDENT_JSON`.