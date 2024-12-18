/*
*	rmx Library
*	Copyright (C) 2008-2024 by Eukaryot
*
*	Published under the GNU GPLv3 open source software license, see license.txt
*	or https://www.gnu.org/licenses/gpl-3.0.en.html
*/

#include "rmxmedia.h"


namespace rmx
{

	/**
	 * Registers a thread with the ThreadManager.
	 * 
	 * This method adds the given thread to the set of managed threads.
	 * It allows the ThreadManager to keep track of and potentially manage
	 * the lifecycle of the registered thread.
	 * 
	 * @param thread The ThreadBase object to be registered
	 */
	void ThreadManager::registerThread(ThreadBase& thread)
	{
		mThreads.insert(&thread);
	}

	void ThreadManager::unregisterThread(ThreadBase& thread)
	{
		mThreads.erase(&thread);
	}


	/**
	 * Constructor for ThreadBase class.
	 * 
	 * Initializes a new ThreadBase instance with a default name and registers
	 * the thread with the thread manager.
	 * 
	 * @return A new ThreadBase instance
	 */
	ThreadBase::ThreadBase() :
		mName("rmx Thread")
	{
		mManager->registerThread(*this);
	}

	/**
	 * Constructor for ThreadBase class.
	 * 
	 * Initializes a new ThreadBase object with the given name and registers
	 * it with the thread manager.
	 * 
	 * @param name The name of the thread
	 */
	ThreadBase::ThreadBase(const std::string& name) :
		mName(name)
	{
		mManager->registerThread(*this);
	}

	/**
	 * Destructor for the ThreadBase class. This method ensures proper cleanup
	 * of the thread, including stopping the thread if it's still running and
	 * unregistering it from the thread manager.
	 * 
	 * If the thread is still running when the destructor is called, it will
	 * signal the thread to stop using the signalStopThread method with a force
	 * flag set to true.
	 * 
	 * Finally, it unregisters the thread from the thread manager to prevent
	 * any further management or interaction with this thread object.
	 */
	ThreadBase::~ThreadBase()
	{
		if (mIsThreadRunning)
		{
			signalStopThread(true);
		}
		mManager->unregisterThread(*this);
	}

	/**
	 * Executes the thread function for a ThreadBase object.
	 * 
	 * This static method is typically used as an entry point for thread execution.
	 * It casts the provided void pointer to a ThreadBase object and calls its
	 * runThreadInternal method.
	 * 
	 * @param data A void pointer to a ThreadBase object.
	 * @return 1 if the thread executed successfully, 0 if the provided pointer was null.
	 */
	int ThreadBase::runThreadStatic(void* data)
	{
		ThreadBase* thread = reinterpret_cast<ThreadBase*>(data);
		if (nullptr == thread)
			return 0;

		thread->runThreadInternal();
		return 1;
	}

	/**
	 * Executes the internal thread logic for ThreadBase.
	 *
	 * This method sets up the thread's running state, executes the main thread
	 * function, and then updates the state flags upon completion. It's responsible
	 * for managing the lifecycle of the thread operation.
	 *
	 * @throws Any exception that might be thrown by the threadFunc() method
	 */
	void ThreadBase::runThreadInternal()
	{
		mIsThreadRunning = true;
		mShouldBeRunning = true;
		threadFunc();
		mShouldBeRunning = false;
		mIsThreadRunning = false;
	}

	/**
	 * Starts the thread if it is not already running.
	 * 
	 * This method creates a new SDL thread using SDL_CreateThread if the thread
	 * is not currently running. It sets the thread name to the stored name (mName)
	 * and uses the static method ThreadBase::runThreadStatic as the thread function.
	 * 
	 * @return void
	 */
	void ThreadBase::startThread()
	{
		if (!mIsThreadRunning)
		{
			mSDLThread = SDL_CreateThread(ThreadBase::runThreadStatic, mName.c_str(), this);
		}
	}

	/**
	 * Signals the thread to stop and optionally waits for it to finish.
	 * 
	 * This method sets the running flag to false, indicating that the thread should stop its execution.
	 * If the 'join' parameter is true, it will also wait for the thread to complete before returning.
	 * 
	 * @param join If true, wait for the thread to finish; if false, just signal it to stop
	 */
	void ThreadBase::signalStopThread(bool join)
	{
		if (mIsThreadRunning)
		{
			mShouldBeRunning = false;
			if (join)
				joinThread();
		}
	}

	/**
	 * Joins the thread if it is currently running.
	 * 
	 * This method waits for the SDL thread to complete its execution if the thread
	 * is running and a valid SDL thread object exists. It uses SDL_WaitThread to
	 * block until the thread finishes.
	 * 
	 * @return This method does not return a value.
	 */
	void ThreadBase::joinThread()
	{
		if (mIsThreadRunning && nullptr != mSDLThread)
		{
			SDL_WaitThread(mSDLThread, nullptr);
		}
	}

}
