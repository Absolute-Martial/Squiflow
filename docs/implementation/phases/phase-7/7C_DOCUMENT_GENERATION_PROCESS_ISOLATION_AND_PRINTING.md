# Phase 7C — Document Generation, Process Isolation, and Printing

## Document semantics

Generated/issued documents retain stable references to the business facts/version that produced them. Generation failure cannot rewrite or roll back already committed business truth.

Document generation or printing concepts may exist earlier. The separate helper-process boundary is introduced only when real parser/native/resource/fault isolation earns it.

## Process isolation

Create an on-demand `SquiFlow.Document` or other helper process only if real PDF/image/native/parser/resource risk justifies fault/resource isolation.

If introduced, immediately define:

- IPC/version contract;
- input/output ownership;
- temp/staging protection;
- timeout/cancellation;
- process crash/restart behavior;
- resource limits;
- cleanup and observability.

## Printing

Local printing is an external physical effect:

```text
committed printable document
→ Windows spooler/printer
→ Success | Failure | Unknown
```

Printer failure never undoes the sale/order/invoice/quotation.

## Security

Treat templates, PDFs/images and user uploads as untrusted inputs. Bound parser/conversion resource usage and prevent unsafe path/template execution.

## Exit gate

Document/native/printing failures are isolated from authoritative business state and ambiguous physical outcomes are represented honestly.