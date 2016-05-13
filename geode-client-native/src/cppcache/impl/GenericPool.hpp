/*=========================================================================
 * Copyright (c) 2010-2014 Pivotal Software, Inc. All Rights Reserved.
 * This product is protected by U.S. and international copyright
 * and intellectual property laws. Pivotal products are covered by
 * one or more patents listed at http://www.pivotal.io/patents.
 *=========================================================================
 */
#if !defined (IMPL_POOL_INCLUDED)
#define IMPL_POOL_INCLUDED

#include <deque>
#include <ace/Guard_T.h>
#include <ace/Thread_Mutex.h>

namespace gemfire
{
 template<class T>
	class CPPCACHE_EXPORT GenericPool {
	public:
		Pool(unsigned long numElements)
		{
			for (unsigned long n = 0; n < numElements; n++)
			{
				T* mp = new T();
				m_freelist.push_front(mp);
			}
		}
		~Pool()
		{
			ACE_Guard< ACE_Thread_Mutex > _guard( m_mutex );
			while (m_freelist.size() > 0)
			{
				T* mp = m_freelist.back();
				m_freelist.pop_back();
				delete mp;
			}
		}
		T* get()
		{
			T* mp = 0;
			ACE_Guard< ACE_Thread_Mutex > _guard( m_mutex );
			if (m_freelist.size() > 0)
			{
				mp = m_freelist.back();
				m_freelist.pop_back();
			}
			/*
			else
			{
				mp = new T();
			}
			*/
			return mp;
		}
		void release(T* mp)
		{
			mp->detach();
			{	ACE_Guard< ACE_Thread_Mutex > _guard( m_mutex );
			  /*
			        if ( m_freelist.size() > 20000 ) {
				  delete mp;
				} else {
				*/
				  m_freelist.push_front(mp);
				  /*
				}
				*/
			}
		}
	private:
		 typedef std::deque<T*> LocalQueue;
		 LocalQueue	m_freelist;
     ACE_Thread_Mutex m_mutex;
	};
} // end namespace
#endif // !defined (IMPL_POOL_INCLUDED)
