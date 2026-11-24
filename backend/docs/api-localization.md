# Language of the Api errors

The Api answers its errors in Brazilian Portuguese, English or Spanish. The code of the error never changes; only the text does.

## Which language

The language comes from the `Accept-Language` header and from nothing else: the query string (`?culture=en`) and cookies are ignored, so a link cannot change the language of an answer.

| Request | Answer |
|---|---|
| No header | pt-BR (the default) |
| `en`, `en-US`, `en-GB` | en |
| `es`, `es-AR`, `es-MX` | es |
| `pt-BR` | pt-BR |
| `es;q=0.9,en;q=0.5` | es (the weight decides, as in any `Accept-Language`) |
| `fr`, `pt-PT`, `*`, a malformed value | pt-BR (the default) |

A regional variant falls back to its language. The answer carries `Content-Language` with the language that was used.

## What is translated

| Part of the problem | Translated? |
|---|---|
| `detail` | Yes. The text of `code`, with `arguments` in `{0}`, `{1}`... |
| `errors[].message` | Yes. The text of `errors[].code` |
| `title` | No. It is the standard phrase of the HTTP status |
| `code`, `arguments`, `errors[].field` | No. They are keys and values for the caller |

A code with no text has no `detail` (the caller still has `code`). The `detail` is never the message of an exception: a failure nobody expected answers the text of `Server.UnexpectedError`.

## Where the texts are

`backend/src/PlanelyApp.Api/Resources` (the `.resx` and the code that reads them):

- `Messages.resx` is pt-BR and the neutral language, `Messages.en.resx` is English and `Messages.es.resx` is Spanish.
- The key is the code (`Customer.NotFound`). The value is the text, with `{0}`, `{1}`... where an argument goes.
- `Http.<Phrase>` codes (`Http.NotFound`, `Http.TooManyRequests`...) have a text too: they are the answer of a status with no use case behind it.
- A text does not say a number of a limit (the limit can change in the code). It says what is wrong.

At runtime, a key missing from `en` or `es` silently falls back to Portuguese. That is why `MessagesCatalogTests` is strict: it fails when a language lacks a code, when a text has no code, when the placeholders differ between languages and when a translation is still a copy of the Portuguese text.

## Adding a code

1. Write the code in the Domain (`DomainException`) or in the use case (`Error`), as `<Type>.<Problem>`.
2. Add the key to the three `.resx`, with the same placeholders in each language.
3. Run `dotnet test`. `MessagesCatalogTests` finds every `"Type.Problem"` literal of `src`; if the literal is not a code (a name of a field, of an option...), add it to `NotCodes` in that test.
4. Add the code to `error-codes.md` or `application-error-codes.md`.

## Known limits

- An argument that is a status (`Pending`, `Completed`) goes into the text as the name of the enum, not translated.
- The reasons of the rows of an import are stored as `Code(arg;arg)`. The texts exist, but the endpoint that shows them translated comes with the import endpoints (5.9).
- The language of an e-mail or a notification comes from the `Language` of the customer or of the company, not from the request. It is a different feature.
