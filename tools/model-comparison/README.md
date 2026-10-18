# Model comparison

Runs a folder of real invoice PDFs through several Claude models, with the
same prompt, schema and parsing as production
(`AnthropicInvoiceInterpreter`). The report shows per invoice:
- the fields a reviewer would otherwise correct, per model, marked where they
  differ from the first (reference) model;
- tokens, cost and time.

```
export ANTHROPIC_API_KEY=...          # never commit it
dotnet run --project tools/model-comparison -- <folder with PDFs> [model ...]
```

Without models it runs all four: `claude-opus-5` (reference), `claude-opus-5-5`,
`claude-sonnet-5-5` and `claude-haiku-5-5`. The report is written to
`<folder>/model-comparison.md`.

- **It costs money.** Every PDF goes to every model. Roughly per PDF: Opus
  $0.09, Sonnet $0.03, Haiku $0.003. The prices in `Program.cs` come from
  public price lists; check Anthropic's pricing page.
- **The PDFs are real customer data.** They go to Anthropic exactly as in
  production. Keep the folder outside the repository.
- **"Differs" is not "wrong".** A ≠ only says two models disagree. Which one is
  right is decided by looking at the PDF.

Choosing a model is done afterwards on the Platform page ("Leesmodel"); this
tool only measures.
