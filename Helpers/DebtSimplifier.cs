using NetSplit.Models;

namespace NetSplit.Helpers
{
    public static class DebtSimplifier
    {
        public static List<UserBalance> CalculateUserBalances(
            List<GroupMember> members,
            List<Expense> expenses,
            List<Settlement> settlements)
        {
            var balanceMap = members.ToDictionary(m => m.UserId, m => new UserBalance
            {
                UserId = m.UserId,
                UserName = m.FullName,
                TotalPaid = 0m,
                TotalOwed = 0m,
                TotalSettlementsPaid = 0m,
                TotalSettlementsReceived = 0m,
                NetBalance = 0m
            });

            // 1. Process Expenses
            foreach (var expense in expenses)
            {
                if (balanceMap.ContainsKey(expense.PaidByUserId))
                {
                    balanceMap[expense.PaidByUserId].TotalPaid += expense.Amount;
                }

                foreach (var share in expense.Shares)
                {
                    if (balanceMap.ContainsKey(share.UserId))
                    {
                        balanceMap[share.UserId].TotalOwed += share.AmountOwed;
                    }
                }
            }

            // 2. Process Settlements (Only Paid settlements alter balances)
            foreach (var settlement in settlements.Where(s => s.Status == SettlementStatus.Paid))
            {
                if (balanceMap.ContainsKey(settlement.PayerUserId))
                {
                    balanceMap[settlement.PayerUserId].TotalSettlementsPaid += settlement.Amount;
                }
                if (balanceMap.ContainsKey(settlement.ReceiverUserId))
                {
                    balanceMap[settlement.ReceiverUserId].TotalSettlementsReceived += settlement.Amount;
                }
            }

            // 3. Compute Net Balances
            foreach (var ub in balanceMap.Values)
            {
                // Net = (Paid + Settlements Paid) - (Owed + Settlements Received)
                ub.NetBalance = (ub.TotalPaid + ub.TotalSettlementsPaid) - (ub.TotalOwed + ub.TotalSettlementsReceived);
            }

            return balanceMap.Values.ToList();
        }

        public static List<SimplifiedDebt> SimplifyDebts(List<UserBalance> balances, string currency = "INR")
        {
            var result = new List<SimplifiedDebt>();

            // Separate creditors (+ balance) and debtors (- balance)
            var creditors = balances.Where(b => b.NetBalance > 0.01m)
                                    .Select(b => new DebtNode { UserId = b.UserId, Name = b.UserName, Amount = b.NetBalance })
                                    .OrderByDescending(c => c.Amount)
                                    .ToList();

            var debtors = balances.Where(b => b.NetBalance < -0.01m)
                                  .Select(b => new DebtNode { UserId = b.UserId, Name = b.UserName, Amount = Math.Abs(b.NetBalance) })
                                  .OrderByDescending(d => d.Amount)
                                  .ToList();

            int debtIdCounter = 1;

            int cIdx = 0;
            int dIdx = 0;

            while (cIdx < creditors.Count && dIdx < debtors.Count)
            {
                var creditor = creditors[cIdx];
                var debtor = debtors[dIdx];

                decimal settlementAmount = Math.Min(creditor.Amount, debtor.Amount);
                settlementAmount = Math.Round(settlementAmount, 2);

                if (settlementAmount > 0)
                {
                    var debt = new SimplifiedDebt
                    {
                        DebtId = debtIdCounter++,
                        DebtorId = debtor.UserId,
                        DebtorName = debtor.Name,
                        CreditorId = creditor.UserId,
                        CreditorName = creditor.Name,
                        Amount = settlementAmount,
                        Currency = currency
                    };

                    // Generate step-by-step explanation
                    debt.ExplanationSteps.Add($"Net Obligation Calculation:");
                    debt.ExplanationSteps.Add($"{debtor.Name} has a net deficit of {currency} {debtor.Amount:N2} across group expenses.");
                    debt.ExplanationSteps.Add($"{creditor.Name} has a net surplus of {currency} {creditor.Amount:N2} paid out-of-pocket.");
                    debt.ExplanationSteps.Add($"By matching peak debtor ({debtor.Name}) with peak creditor ({creditor.Name}), an intermediate transfer of {currency} {settlementAmount:N2} directly clears/reduces both positions.");
                    debt.ExplanationSteps.Add($"Resulting direct settlement: {debtor.Name} pays {creditor.Name} {currency} {settlementAmount:N2}.");

                    result.Add(debt);

                    creditor.Amount -= settlementAmount;
                    debtor.Amount -= settlementAmount;
                }

                if (creditor.Amount <= 0.001m) cIdx++;
                if (debtor.Amount <= 0.001m) dIdx++;
            }

            return result;
        }

        private class DebtNode
        {
            public int UserId { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Amount { get; set; }
        }
    }
}
