using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// A circular list implementation that will automatically cycle around to the next position in the list without going over.
/// </summary>
/// <typeparam name="T">The type to store in the list.</typeparam>
public class CircularList<T> : List<T>
{
    private int position = 0;
    private bool _moveForward = true;
    /// <summary>
    /// Returns the current index of the list.
    /// </summary>
    /// <remarks>
    /// To set the position use the <see cref="SetPosition(int)"/> method.
    /// </remarks>
    public int Position { get { return position; } }

    /// <summary>
    /// Set the starting position of the circular list.
    /// </summary>
    /// <remarks>
    /// This will clamp the value to either 0 or the max value of the list.
    /// </remarks>
    /// <param name="startPosition">What element index to start from.</param>
    public void SetPosition(int startPosition)
    {
        position = MathExtension.Clamp(startPosition, 0, Count - 1);
    }

    public new void Reverse()
    {
        _moveForward = !_moveForward;
    }

    /// <summary>
    /// Advance to the next position in the list.
    /// </summary>
    /// <returns>A <see cref="T"/> object.</returns>
    public T Next()
    {
        return ReverseImpl(_moveForward);
    }

    private T ReverseImpl(bool moveForward)
    {
        if (moveForward)
        {
            if (++position >= this.Count)
            {
                position = 0;
            }
        }
        else
        {
            if (--position < 0)
            {
                position = Count - 1;
            }
        }
        return this[position];

    }

    /// <summary>
    /// Advance to the previous position in the list.
    /// </summary>
    /// <returns>A <see cref="T"/> object.</returns>
    public T Prev()
    {
        return ReverseImpl(!_moveForward);
    }

    public T PeekNext()
    {
        int nextPosition = position;
        if (_moveForward)
        {
            nextPosition = position + 1;
            if (nextPosition >= this.Count)
            {
                nextPosition = 0;
            }
        }
        else
        {
            nextPosition = position - 1;
            if (nextPosition < 0)
            {
                nextPosition = Count - 1;
            }
        }
        return this[nextPosition];

    }


    /// <summary>
    /// The object at the current position in the list.
    /// </summary>
    /// <returns>A <see cref="T"/> object.</returns>
    public T Current()
    {
        return this[position];
    }
}
