//2010, CPOL, Stan Kirk
//2015, MIT, EngineKit

using System;
using System.Collections.Generic;
using System.Threading;

namespace SharpConnect
{

    sealed class SharedResoucePool<T>
    {
        //just for assigning an ID so we can watch our objects while testing. 
        // Pool of reusable SocketAsyncEventArgs objects.        
        Stack<T> _pool;
        // initializes the object pool to the specified size.
        // "capacity" = Maximum number of SocketAsyncEventArgs objects
        public SharedResoucePool(int capacity)
        {

#if DEBUG
            if (dbugLOG.watchProgramFlow)   //for testing
            {
                dbugLOG.WriteLine("SocketAsyncEventArgsPool constructor");
            }
#endif

            this._pool = new Stack<T>(capacity);
        }

        // The number of SocketAsyncEventArgs instances in the pool.         
        internal int Count => this._pool.Count;


        // Removes a SocketAsyncEventArgs instance from the pool.
        // returns SocketAsyncEventArgs removed from the pool.
        internal T Pop()
        {
            lock (this._pool)
            {
                return this._pool.Pop();
            }
        }

        // Add a SocketAsyncEventArg instance to the pool. 
        // "item" = SocketAsyncEventArgs instance to add to the pool.
        internal void Push(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null");
            }
            lock (this._pool)
            {
                this._pool.Push(item);
            }
        }
#if DEBUG
        Int32 dbugNextTokenId = 0;
        internal Int32 dbugGetNewTokenId()
        {
            return Interlocked.Increment(ref dbugNextTokenId);
        }
#endif


    }

}
