using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;

namespace ModTest.Scripts.Planning;

public sealed class PlanningCardSelector : ICardSelector
{
    public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
    {
        if (!PlannerRuntime.IsEnabled)
        {
            return Task.FromResult(Enumerable.Empty<CardModel>());
        }

        PlanningSnapshot snapshot = PlannerRuntime.BuildSnapshot();
        IReadOnlyList<CardModel> cards = options.ToList();
        IReadOnlyList<CardModel> selection = PlannerRuntime.Policy.PlanCardSelection(snapshot, cards, minSelect, maxSelect);
        return Task.FromResult<IEnumerable<CardModel>>(selection);
    }

    public CardModel? GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
    {
        if (!PlannerRuntime.IsEnabled)
        {
            return null;
        }

        PlanningSnapshot snapshot = PlannerRuntime.BuildSnapshot();
        return PlannerRuntime.Policy.PlanCardReward(snapshot, options.Select(static option => option.Card).ToList()).Card;
    }
}
