using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CardAnimator : MonoBehaviour
{
    /// <summary>
    /// Card animator that takes care of LERPing the cards to the specific location.
    /// </summary>
    /// <param name="targetTo">The transform the card should go to</param>
    /// <returns>true if the animation completed; false if the target was null</returns>
    public bool AnimateToPosition(UnityEngine.Transform targetTo)
    {
        Transform cardToMoveToTarget = this.transform;
        Transform toTarget = targetTo;

        if (toTarget != null)
        {
            return AnimateToPosition(transform.position, transform.rotation);
        }
        return false;
    }

    /// <summary>
    /// Card animator that takes care of LERPing the cards to the specific location.
    /// </summary>
    /// <param name="targetTo">The transform the card should go to</param>
    /// <returns>true if the animation completed; false if the target was null</returns>
    public bool AnimateToPosition(Vector3 position, Quaternion rotation)
    {
        Transform cardToMoveToTarget = this.transform;

        if (position != null)
        {

            // Move our position a step closer to the target.
            // To make sure someone with a REAAALLY large screen doesn't have a disadvantage when someone is using a small screen
            // In this example we'll do all of our animations based on the fact that speed/distance = the time to animate one distance
            float elapsedTime = 0;
            float waitTime = 250;

            while (elapsedTime < waitTime)
            {
                cardToMoveToTarget.position = Vector3.Lerp(cardToMoveToTarget.position, position, (elapsedTime / waitTime));
                cardToMoveToTarget.rotation = Quaternion.Lerp(cardToMoveToTarget.rotation, rotation, (elapsedTime / waitTime));
                elapsedTime += Time.deltaTime;
            }
        }
        else
        {
            return false;
        }
        return true;
    }
}
