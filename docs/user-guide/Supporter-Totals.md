# Supporter totals

The **Financial ledger** records supported contribution events and their
valuations. It is a stream display record, not a payout statement. A platform's
reported contribution and the amount ultimately paid to you can differ.

## Read the totals

1. Open **Financial ledger**.
2. Choose **Totals period**: all-time, current-stream, today, week, month, year
   or custom.
3. For current-stream totals, set and save the stream start first. For custom
   dates, the end date is exclusive: a range ending April 2 stops before April 2.
4. Use **Ledger state** to examine counted, pending, gated or excluded records.

Daily periods use your saved financial timezone; weeks start Monday. Use
**Refresh financial data** if you want to reload the current view.

Three valuation descriptions matter:

- **Exact**: the event supplies an amount and currency.
- **Nominal**: a configured estimate supplies the value, such as your chosen
  subscription value per unit.
- **Unknown**: no supported amount or configured valuation is available.

Unknown values do not silently become money. Foreign-currency conversion may
remain pending until a historical rate or manual override is available.
Unverified gift or identity behavior stays gated rather than being presented as
proven support.

## Configure estimates carefully

Under **Nominal supporter values**, choose a platform, support type, tier where
needed, and **USD per unit**. Click **Save nominal value**. An estimate is your
explicit choice; label it honestly when using totals on stream.

Changing valuation settings does not mean every historical record has already
been rewritten. Review applicable counted contributions, select them and use
**Reconcile selected valuations** when you intend to apply current valuation
evidence. Read the reported outcomes; missing rates or rules can still leave a
value unknown. Download a backup before changing large sets of historical data.

## Link the same supporter across platforms

Identity linking lets one person's known platform identities contribute to the
same supporter total. Link only identities you have evidence belong together.
Matching display names alone are not proof. Review the resulting totals after a
link or unlink.

## Show supporter widgets

In the visual editor, add Donor Crown, a ranked leaderboard, latest supporter or
current-stream totals as needed. Select the widget and configure its filters,
period and appearance. Copy the overlay URL into an OBS Browser Source.

Changes to linked identities and reconciled valuations update the live views.
Check the actual OBS result. Isolated test events do not add production
contributions, so use preview examples for appearance and real supported events
for ingestion qualification.
