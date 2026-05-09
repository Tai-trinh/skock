# rustfmt as the shared Rust formatter

All Rust source files are formatted with `rustfmt` using the project's `rustfmt.toml` as the single source of truth. Run before committing, or configure your editor to format on save (`rust-analyzer` does this by default). CI should reject PRs where `cargo fmt --check` reports a diff.

When `rustfmt` would damage readability — aligned columns, hand-tuned tables, or bit-flag layouts — wrap the block with `#[rustfmt::skip]` on the item, or the attribute block `#[rustfmt::skip]` / `// rustfmt::skip` guards for inline regions. The skip must be as narrow as possible and accompanied by a comment explaining why manual formatting is clearer here.
