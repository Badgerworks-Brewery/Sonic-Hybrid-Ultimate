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
	 * Constructor for the JobManager class.
	 * 
	 * Initializes the condition variable and mutex lock for job management.
	 * 
	 * @return None
	 */
	JobManager::JobManager()
	{
		mConditionVariable = SDL_CreateCond();
		mConditionLock = SDL_CreateMutex();
	}

	JobManager::~JobManager()
	{
		stopAllThreads();
		SDL_DestroyCond(mConditionVariable);
		SDL_DestroyMutex(mConditionLock);
	}

	/**
	 * Sets the maximum number of threads for the JobManager.
	 * The input value is clamped between 0 and 16.
	 * 
	 * @param count The desired number of maximum threads
	 */
	void JobManager::setMaxThreads(int count)
	{
		mMaxThreads = clamp(count, 0, 16);
	}

	/**
	 * Inserts a job into the job manager for processing.
	 * 
	 * This method registers a job with the JobManager and prepares it for execution. If the job is already 
	 * registered, it resets its state. If not, it registers the job and ensures there are enough worker 
	 * threads available. The job is then added to the queue and a worker thread is signaled to process it. 
	 * If no worker threads are available, the job is executed on the calling thread.
	 * 
	 * @param job The JobBase object to be inserted and processed
	 * @return void
	 */
	void JobManager::insertJob(JobBase& job)
	{
		if (nullptr != job.mRegisteredAtManager)
		{
			if (job.mRegisteredAtManager != this)
				return;
			if (job.mJobState != JobBase::JobState::INACTIVE && job.mJobState != JobBase::JobState::DONE)
				return;

			// Job is already registered here, but needs to have its state reset back to waiting
		}
		else
		{
			// Register job here
			job.mRegisteredAtManager = this;

			// Make sure we have enough worker threads
			//  -> TODO: Only create a new one if all existing threads are actually busy
			const int index = (int)mThreads.size();
			if (index < mMaxThreads)
			{
				JobWorkerThread* thread = new JobWorkerThread(*this, index);
				mThreads.push_back(thread);
				thread->startThread();
			}
		}

		// Job is ready to be processed
		job.mJobState = JobBase::JobState::WAITING;

		// Wake up a thread
		if (!mThreads.empty())
		{
			SDL_LockMutex(mConditionLock);
			mJobs.push_back(&job);
			SDL_CondSignal(mConditionVariable);
			SDL_UnlockMutex(mConditionLock);
		}
		else
		{
			// In case there are no worker threads, execute on the calling thread
			job.executeOnCallingThread();
		}
	}

	/**
	 * Inserts a job into the job manager with a specified priority.
	 * 
	 * @param job The JobBase object to be inserted
	 * @param priority The float value representing the priority of the job
	 */
	void JobManager::insertJob(JobBase& job, float priority)
	{
		job.mJobPriority = priority;
		insertJob(job);
	}

	/**
	 * Removes a job from the job manager and ensures it's no longer executing.
	 * 
	 * This method removes the specified job from the internal job list if it's registered
	 * with this manager. If the job is successfully removed, it waits for the job to
	 * finish executing before returning. The method uses mutex locks to ensure thread safety.
	 * 
	 * @param job The job to be removed from the manager
	 * @return void
	 */
	void JobManager::removeJob(JobBase& job)
	{
		if (job.mRegisteredAtManager != this)
			return;

		bool wasRemoved = false;
		SDL_LockMutex(mConditionLock);
		for (size_t i = 0; i < mJobs.size(); ++i)
		{
			if (mJobs[i] == &job)
			{
				// Swap with last
				if (i + 1 < mJobs.size())
				{
					mJobs[i] = mJobs.back();
				}
				mJobs.pop_back();
				wasRemoved = true;
			}
		}
		job.mRegisteredAtManager = nullptr;
		SDL_UnlockMutex(mConditionLock);

		if (wasRemoved)
		{
			// Wait until job execution is done
			job.mJobPriority = -1.0f;
			job.mJobShouldBeRunning = false;
			while (job.mJobState == JobBase::JobState::RUNNING)
			{
				SDL_Delay(1);
			}
		}
	}

	/**
	 * Gets the count of finished jobs in the job manager.
	 * 
	 * This method iterates through all jobs in the job manager and counts
	 * the number of jobs that have been completed. It uses mutex locking
	 * to ensure thread-safe access to the job list.
	 * 
	 * @return The number of finished jobs.
	 */
	int JobManager::getFinishedCount()
	{
		int count = 0;
		SDL_LockMutex(mConditionLock);
		for (JobBase* job : mJobs)
		{
			if (job->isJobDone())
				++count;
		}
		SDL_UnlockMutex(mConditionLock);
		return count;
	}

	/**
	 * Retrieves the next available job from the job queue in a thread-safe manner.
	 * 
	 * This method uses mutex locking to ensure thread safety when accessing
	 * the shared job queue. It acquires a lock, retrieves the next job
	 * using an internal method, and then releases the lock before returning.
	 * 
	 * @return A pointer to the next JobBase object in the queue, or nullptr if the queue is empty.
	 */
	JobBase* JobManager::getNextJob()
	{
		SDL_LockMutex(mConditionLock);
		JobBase* job = getNextJobInternal();
		SDL_UnlockMutex(mConditionLock);
		return job;
	}

	/**
	 * Retrieves the next available job from the job queue, blocking until a job becomes available.
	 * 
	 * This method waits for a job to become available in the queue. If no job is immediately
	 * available, it will block the calling thread until a job is ready or until the search for
	 * jobs is terminated. The method uses a mutex and condition variable to manage thread
	 * synchronization and implements a timeout mechanism to periodically check for delayed jobs
	 * and the running state.
	 * 
	 * @return A pointer to the next JobBase object in the queue, or nullptr if the search for
	 *         jobs has been terminated.
	 */
	JobBase* JobManager::getNextJobBlocking()
	{
		// Wait until there's at leat one job available
		SDL_LockMutex(mConditionLock);
		JobBase* job = getNextJobInternal();
		while (nullptr == job && mSearchforJobs)
		{
			// Using a time-out for two reasons:
			//  - to have a chance to check if "mShouldBeRunning" changed outside
			//  - to react to a delayed job, if there's no other jobs at the moment
			uint32 timeoutMilliseconds = 100;
			if (mNextDelayedJobTicks != 0xffffffff)
			{
				const uint32 currentTicks = SDL_GetTicks();
				if (mNextDelayedJobTicks > currentTicks)
					timeoutMilliseconds = mNextDelayedJobTicks - currentTicks;
			}
			SDL_CondWaitTimeout(mConditionVariable, mConditionLock, timeoutMilliseconds);
			job = getNextJobInternal();
		}
		SDL_UnlockMutex(mConditionLock);
		return job;
	}

	/**
	 * Retrieves the list of jobs from the JobManager.
	 * 
	 * This method provides a thread-safe way to access the current list of jobs
	 * managed by the JobManager. It uses SDL mutex locking to ensure
	 * thread safety when accessing the shared job list.
	 * 
	 * @param output A reference to a vector of JobBase pointers that will be
	 *               filled with the current list of jobs.
	 * @return void This method doesn't return a value, but modifies the
	 *         output parameter.
	 */
	void JobManager::getJobList(std::vector<JobBase*>& output)
	{
		SDL_LockMutex(mConditionLock);
		output = mJobs;
		SDL_UnlockMutex(mConditionLock);
	}

	void JobManager::onJobChanged()
	{
		SDL_LockMutex(mConditionLock);
		SDL_CondSignal(mConditionVariable);
		SDL_UnlockMutex(mConditionLock);
	}

	/**
	 * Selects and returns the next job to be executed from the job queue.
	 * 
	 * This method iterates through the job queue to find the waiting job with the highest priority
	 * that is ready to run (not delayed). It also updates the next delayed job execution time.
	 * Jobs with priorities below 0.0f are ignored.
	 * 
	 * @return A pointer to the selected JobBase object, or nullptr if no suitable job is found
	 */
	JobBase* JobManager::getNextJobInternal()
	{
		// Select waiting job with highest priority
		mNextDelayedJobTicks = 0xffffffff;	// This will get updated as well
		JobBase* bestJob = nullptr;
		const uint32 currentTicks = SDL_GetTicks();
		for (JobBase* job : mJobs)
		{
			if (job->mJobState == JobBase::JobState::WAITING)
			{
				if (job->mJobDelayUntilTicks <= currentTicks)
				{
					if (nullptr == bestJob || job->mJobPriority > bestJob->mJobPriority)
					{
						bestJob = job;
					}
				}
				else
				{
					if (job->mJobPriority >= 0.0f && job->mJobDelayUntilTicks < mNextDelayedJobTicks)
					{
						mNextDelayedJobTicks = job->mJobDelayUntilTicks;
					}
				}
			}
		}
		if (nullptr != bestJob)
		{
			// Ignore priorities below 0.0f
			if (bestJob->mJobPriority >= 0.0f)
			{
				bestJob->mJobShouldBeRunning = true;
				bestJob->mJobState = JobBase::JobState::RUNNING;
			}
			else
			{
				bestJob = nullptr;
			}
		}
		return bestJob;
	}

	/**
	 * Stops all worker threads managed by the JobManager.
	 * 
	 * This method performs the following steps:
	 * 1. Sets the job search flag to false.
	 * 2. Signals all threads to stop.
	 * 3. Waits for all threads to finish (join).
	 * 4. Deletes all thread objects.
	 * 5. Clears the thread container.
	 * 
	 * @return void
	 */
	void JobManager::stopAllThreads()
	{
		mSearchforJobs = false;
		for (JobWorkerThread* thread : mThreads)
		{
			thread->signalStopThread(false);
		}
		for (JobWorkerThread* thread : mThreads)
		{
			thread->joinThread();
		}
		for (JobWorkerThread* thread : mThreads)
		{
			delete thread;
		}
		mThreads.clear();
	}



	/**
	 * Sets the priority of the job and manages its activation status.
	 * 
	 * This method updates the job's priority and handles the activation/deactivation
	 * of the job based on the priority value. If the job transitions from an inactive
	 * to an active state, it notifies the registered manager to handle the change.
	 * 
	 * @param priority The new priority value for the job. A negative value deactivates the job,
	 *                 while a non-negative value activates it.
	 * @return void
	 */
	void JobBase::setJobPriority(float priority)
	{
		// Jobs with negative priority are deactivated, i.e. won't get processed
		//  -> But when this changes, a thread possibly needs to be woken up
		const bool wakeUpThread = (mJobPriority < 0.0f && priority >= 0.0f);
		mJobPriority = priority;

		if (wakeUpThread && nullptr != mRegisteredAtManager)
		{
			mRegisteredAtManager->onJobChanged();
		}
	}

    /**
     * Sets the delay for the job execution until a specified SDL tick count.
     * 
     * This method updates the job delay time and potentially wakes up a thread
     * if the delay is reduced. If the job is registered with a manager, it
     * notifies the manager of the change.
     * 
     * @param sdlTicks The SDL tick count until which the job should be delayed
     */
    void JobBase::setJobDelayUntilTicks(uint32 sdlTicks)
    {
		// If the job delay gets reduced (possibly to zero, i.e. deactivating the delay), a thread possibly needs to be woken up
		const bool wakeUpThread = (sdlTicks < mJobDelayUntilTicks);
		mJobDelayUntilTicks = sdlTicks;

		if (wakeUpThread && nullptr != mRegisteredAtManager)
		{
			mRegisteredAtManager->onJobChanged();
		}
	}

	/**
	 * Executes the job function on the calling thread.
	 * 
	 * This method sets the job state to RUNNING, executes the job function once,
	 * and then updates the job state based on the result. If the job function
	 * returns true, the job state is set to DONE. Otherwise, it's set back to
	 * WAITING.
	 * 
	 * @return true if the job function completed successfully, false otherwise
	 */
	bool JobBase::callJobFuncOnCallingThread()
	{
		mJobShouldBeRunning = true;
		mJobState = JobBase::JobState::RUNNING;

		// Call job function implementation once
		const bool result = jobFunc();
		if (result)
		{
			// Job is done
			mJobState = JobBase::JobState::DONE;
		}
		else
		{
			// Set back to waiting state
			mJobState = JobBase::JobState::WAITING;
		}
		return result;
	}

	/**
	 * Executes the job on the calling thread.
	 * 
	 * This method runs the job if it is in a WAITING state or earlier. It sets the job state to RUNNING,
	 * executes the job function repeatedly until it returns true, and then sets the job state to DONE.
	 * If the job is already in a state beyond WAITING, this method does nothing.
	 * 
	 * @return void
	 */
	void JobBase::executeOnCallingThread()
	{
		if (mJobState <= JobBase::JobState::WAITING)
		{
			mJobShouldBeRunning = true;
			mJobState = JobBase::JobState::RUNNING;

			// Execute until done
			while (!jobFunc())
			{
			}

			mJobShouldBeRunning = false;
			mJobState = JobBase::JobState::DONE;
		}
	}



	/**
	 * Constructor for JobWorkerThread class.
	 * 
	 * @param jobManager Reference to the JobManager object
	 * @param index An integer index (currently unused)
	 */
	JobWorkerThread::JobWorkerThread(JobManager& jobManager, int index) :
		mJobManager(jobManager)
	{
		// Index goes unused at the moment
	}

	/**
	 * Thread function for the JobWorkerThread class.
	 * 
	 * This method implements the main loop of the worker thread. It continuously
	 * retrieves jobs from the job manager and executes them. If a job is successfully
	 * completed, it is marked as done and removed from the manager. If a job fails,
	 * it is set back to a waiting state. The thread continues to run until
	 * mShouldBeRunning is set to false.
	 * 
	 * @return This method does not return a value (void).
	 */
	void JobWorkerThread::threadFunc()
	{
		while (mShouldBeRunning)
		{
			JobBase* job = mJobManager.getNextJobBlocking();
			if (nullptr != job)
			{
				// Execute job
				const bool result = job->jobFunc();
				if (result)
				{
					// Job is done
					job->mJobState = JobBase::JobState::DONE;
					job->mRegisteredAtManager->removeJob(*job);
				}
				else
				{
					// Set back to waiting state
					//  -> Note that the job's priority might have changed, or there's another job with higher priority now, so don't just continue with this job
					job->mJobState = JobBase::JobState::WAITING;
				}
			}
		}
	}

}
