//=========================================================================
// Copyright (c) 2002-2014 Pivotal Software, Inc. All Rights Reserved.
// This product is protected by U.S. and international copyright
// and intellectual property laws. Pivotal products are covered by
// more patents listed at http://www.pivotal.io/patents.
//========================================================================

using System;
using System.Collections.Generic;
using System.Threading;

namespace GemStone.GemFire.Cache.UnitTests
{
  using NUnit.Framework;
  using GemStone.GemFire.DUnitFramework;
  using GemStone.GemFire.Cache.Tests;

  [TestFixture]
  [Category("group2")]
  [Category("unicast_only")]
  [Category("deprecated")]
  public class ThinClientDurableTests : ThinClientRegionSteps
  {
    #region Private members

    private UnitProcess m_client1, m_client2, m_feeder;
    private string[] m_regexes = { "D-Key-.*", "Key-.*" };
    private string[] m_mixKeys = { "Key-1", "D-Key-1", "L-Key", "LD-Key" };
    private string[] keys = { "Key-1", "Key-2", "Key-3", "Key-4", "Key-5" };

    private static string DurableClientId1 = "DurableClientId1";
    private static string DurableClientId2 = "DurableClientId2";

    private static DurableListener m_checker1, m_checker2;

    #endregion

    protected override ClientBase[] GetClients()
    {
      m_client1 = new UnitProcess();
      m_client2 = new UnitProcess();
      m_feeder = new UnitProcess();
      return new ClientBase[] { m_client1, m_client2, m_feeder };
    }

    [TestFixtureTearDown]
    public override void EndTests()
    {
      CacheHelper.StopJavaServers();
      base.EndTests();
    }

    [TearDown]
    public override void EndTest()
    {
      try
      {
        m_client1.Call(CacheHelper.Close);
        m_client2.Call(CacheHelper.Close);
        m_feeder.Call(CacheHelper.Close);
        CacheHelper.ClearEndpoints();
        CacheHelper.ClearLocators();
      }
      finally
      {
        CacheHelper.StopJavaServers();
      }
      base.EndTest();
    }

    #region Common Functions

    public void InitFeeder(string endpoints, string locators, int redundancyLevel, bool pool, bool locator)
    {
      if (pool)
      {
        if (locator)
        {
          CacheHelper.CreatePool("__TESTPOOL1_", (string)null, locators, (string)null, redundancyLevel, false);
          CacheHelper.CreateTCRegion_Pool(RegionNames[0], false, true, null, (string)null,
            locators, "__TESTPOOL1_", false);
        }
        else
        {
          CacheHelper.CreatePool("__TESTPOOL1_", endpoints, (string)null, (string)null, redundancyLevel, false);
          CacheHelper.CreateTCRegion_Pool(RegionNames[0], false, true, null, endpoints,
            (string)null, "__TESTPOOL1_", false);
        }
      }
      else
      {
        CacheHelper.InitConfig(endpoints, redundancyLevel);
        CacheHelper.CreateTCRegion(RegionNames[0], false, true, null, CacheHelper.Endpoints, false);
      }
    }

    public void ClearChecker(int client)
    {
      if (client == 1)
      {
        ThinClientDurableTests.m_checker1 = null;
      }
      else // client == 2
      {
        ThinClientDurableTests.m_checker2 = null;
      }
    }

    public void InitDurableClient(int client, string endpoints, string locators, int redundancyLevel,
      string durableClientId, int durableTimeout, bool pool, bool locator)
    {
      // Create DurableListener for first time and use same afterward.
      DurableListener checker = null;
      if (client == 1)
      {
        if (ThinClientDurableTests.m_checker1 == null)
        {
          ThinClientDurableTests.m_checker1 = DurableListener.Create();
        }
        checker = ThinClientDurableTests.m_checker1;
      }
      else // client == 2 
      {
        if (ThinClientDurableTests.m_checker2 == null)
        {
          ThinClientDurableTests.m_checker2 = DurableListener.Create();
        }
        checker = ThinClientDurableTests.m_checker2;
      }
      if (pool)
      {
        if (locator)
        {
          CacheHelper.InitConfigForDurable_Pool((string)null, locators, redundancyLevel,
            durableClientId, durableTimeout);
          CacheHelper.CreateTCRegion_Pool(RegionNames[0], false, true, checker,
            (string)null, CacheHelper.Locators, "__TESTPOOL1_", true);
        }
        else
        {
          CacheHelper.InitConfigForDurable_Pool(endpoints, (string)null, redundancyLevel,
            durableClientId, durableTimeout);
          CacheHelper.CreateTCRegion_Pool(RegionNames[0], false, true, checker,
            CacheHelper.Endpoints, (string)null, "__TESTPOOL1_", true);
        }
      }
      else
      {
        CacheHelper.InitConfigForDurable(endpoints, redundancyLevel, durableClientId, durableTimeout);
        CacheHelper.CreateTCRegion(RegionNames[0], false, true, checker, CacheHelper.Endpoints, true);
      }
      CacheHelper.DCache.ReadyForEvents();
      Region region1 = CacheHelper.GetVerifyRegion(RegionNames[0]);
      region1.RegisterRegex(m_regexes[0], true);
      region1.RegisterRegex(m_regexes[1], false);
      CacheableKey[] ldkeys = { new CacheableString(m_mixKeys[3]) };
      region1.RegisterKeys(ldkeys, true, false);
      CacheableKey[] lkeys = { new CacheableString(m_mixKeys[2]) };
      region1.RegisterKeys(lkeys, false, false);
    }

    public void InitClientXml(string cacheXml)
    {
      CacheHelper.InitConfig(cacheXml);
    }

    public void ReadyForEvents()
    {
      CacheHelper.DCache.ReadyForEvents();
    }

    public void FeederUpdate(int value, int sleep)
    {
      Region region1 = CacheHelper.GetVerifyRegion(RegionNames[0]);

      region1.Put(m_mixKeys[0], new CacheableInt32(value));
      Thread.Sleep(sleep);
      region1.Put(m_mixKeys[1], new CacheableInt32(value));
      Thread.Sleep(sleep);
      region1.Put(m_mixKeys[2], new CacheableInt32(value));
      Thread.Sleep(sleep);
      region1.Put(m_mixKeys[3], new CacheableInt32(value));
      Thread.Sleep(sleep);

      region1.Destroy(m_mixKeys[0]);
      Thread.Sleep(sleep);
      region1.Destroy(m_mixKeys[1]);
      Thread.Sleep(sleep);
      region1.Destroy(m_mixKeys[2]);
      Thread.Sleep(sleep);
      region1.Destroy(m_mixKeys[3]);
      Thread.Sleep(sleep);
    }

    public void ClientDown(bool keepalive)
    {
      if (keepalive)
      {
        CacheHelper.CloseKeepAlive();
      }
      else
      {
        CacheHelper.Close();
      }
    }

    public void CrashClient()
    {
      // TODO:  crash client here.
    }

    public void KillServer()
    {
      CacheHelper.StopJavaServer(1);
      Util.Log("Cacheserver 1 stopped.");
    }

    public delegate void KillServerDelegate();

    #endregion


    public void VerifyTotal(int client, int keys, int total)
    {
      DurableListener checker = null;
      if (client == 1)
      {
        checker = ThinClientDurableTests.m_checker1;
      }
      else // client == 2
      {
        checker = ThinClientDurableTests.m_checker2;
      }

      if (checker != null)
      {
        checker.validate(keys, total);
      }
      else
      {
        Assert.Fail("Checker is NULL!");
      }
    }

    public void VerifyBasic(int client, int keyCount, int eventCount, int durableValue, int nonDurableValue)
    {
      DurableListener checker = null;
      if (client == 1)
      {
        checker = ThinClientDurableTests.m_checker1;
      }
      else // client == 2
      {
        checker = ThinClientDurableTests.m_checker2;
      }

      if (checker != null)
      {
        try
        {
          checker.validateBasic(keyCount, eventCount, durableValue, nonDurableValue);
        }
        catch (AssertionException e)
        {
          Util.Log("VERIFICATION FAILED for client {0}: {1} ",client,e);
          throw e;
        }
      }
      else
      {
        Assert.Fail("Checker is NULL!");
      }
    }

    #region Basic Durable Test


    void runDurableAndNonDurableBasic(bool pool, bool locator)
    {
      CacheHelper.SetupJavaServers(pool && locator,
        "cacheserver_notify_subscription.xml", "cacheserver_notify_subscription2.xml");

      if (pool && locator)
      {
        CacheHelper.StartJavaLocator(1, "GFELOC");
      }

      for (int redundancy = 0; redundancy <= 1; redundancy++)
      {
        for (int closeType = 1; closeType <= 2; closeType++)  
        {
          for (int downtime = 0; downtime <= 1; downtime++) // downtime updates
          {
            Util.Log("Starting loop with closeType = {0}, redundancy = {1}, downtime = {2} ",closeType,redundancy, downtime );

            if (pool && locator)
            {
              CacheHelper.StartJavaServerWithLocators(1, "GFECS1", 1);
            }
            else
            {
              CacheHelper.StartJavaServer(1, "GFECS1");
            }
            Util.Log("Cacheserver 1 started.");

            if (redundancy == 1)
            {
              if (pool && locator)
              {
                CacheHelper.StartJavaServerWithLocators(2, "GFECS2", 1);
              }
              else
              {
                CacheHelper.StartJavaServer(2, "GFECS2");
              }
              Util.Log("Cacheserver 2 started.");
            }

            m_feeder.Call(InitFeeder, CacheHelper.Endpoints, CacheHelper.Locators, 0, pool, locator);
            Util.Log("Feeder initialized.");

            m_client1.Call(ClearChecker, 1);
            m_client2.Call(ClearChecker, 2);

            m_client1.Call(InitDurableClient, 1, CacheHelper.Endpoints, CacheHelper.Locators, redundancy, DurableClientId1, 300, pool, locator);
            m_client2.Call(InitDurableClient, 2, CacheHelper.Endpoints, CacheHelper.Locators, redundancy, DurableClientId2, 30, pool, locator);
            Util.Log("Clients initialized.");

            m_feeder.Call(FeederUpdate, 1, 10);
            Util.Log("Feeder performed first update.");
            Thread.Sleep(45000); // wait for HA Q to drain and notify ack to go out.

            switch (closeType)
            {
              case 1:
                m_client1.Call(ClientDown, true);
                m_client2.Call(ClientDown, true);
                Util.Log("Clients downed with keepalive true.");
                break;
              case 2:
                m_client1.Call(ClientDown, false);
                m_client2.Call(ClientDown, false);
                Util.Log("Clients downed with keepalive false.");
                break;
              case 3:
                m_client1.Call(CrashClient);
                m_client2.Call(CrashClient);
                Util.Log("Clients downed as crash.");
                break;
              default:
                break;
            }

            if (downtime == 1)
            {
              m_feeder.Call(FeederUpdate, 2, 10);
              Util.Log("Feeder performed update during downtime.");
              Thread.Sleep(20000); // wait for HA Q to drain and notify ack to go out.
            }

            m_client1.Call(InitDurableClient, 1, CacheHelper.Endpoints, CacheHelper.Locators, redundancy, DurableClientId1, 300, pool, locator);

            // Sleep for 45 seconds since durable timeout is 30 seconds so that client2 times out
            Thread.Sleep(45000);

            m_client2.Call(InitDurableClient, 2, CacheHelper.Endpoints, CacheHelper.Locators, redundancy, DurableClientId2, 30, pool, locator);

            Util.Log("Clients brought back up.");

            if (closeType != 2 && downtime == 1)
            {
              m_client1.Call(VerifyBasic, 1, 4, 12, 2, 1);
              m_client2.Call(VerifyBasic, 2, 4, 8, 1, 1);
            }
            else
            {
              m_client1.Call(VerifyBasic, 1, 4, 8, 1, 1);
              m_client2.Call(VerifyBasic, 2, 4, 8, 1, 1);
            }

            Util.Log("Verification completed.");

            m_feeder.Call(ClientDown, false);
            m_client1.Call(ClientDown, false);
            m_client2.Call(ClientDown, false);
            Util.Log("Feeder and Clients closed.");

            CacheHelper.StopJavaServer(1);
            Util.Log("Cacheserver 1 stopped.");

            if (redundancy == 1)
            {
              CacheHelper.StopJavaServer(2);
              Util.Log("Cacheserver 2 stopped.");
            }

            Util.Log("Completed loop with closeType = {0}, redundancy = {1}, downtime = {2} ", closeType, redundancy, downtime);

          } // end for int downtime
        } // end for int closeType
      } // end for int redundancy
      if (pool && locator)
      {
        CacheHelper.StopJavaLocator(1);
      }
    }

    // Basic Durable Test to check durable event recieving for different combination
    // of Close type ( Keep Alive = true / false ) , Intermediate update and rudundancy
    [Test]
    public void DurableAndNonDurableBasic()
    {
      runDurableAndNonDurableBasic(false, false); // region config
      runDurableAndNonDurableBasic(true, false); // pool with server endpoints
      runDurableAndNonDurableBasic(true, true); // pool with locator
    } // end [Test] DurableAndNonDurableBasic

    #endregion

    #region Durable Intrest Test

    public void InitDurableClientRemoveInterest(int client, string endpoints, string locators,
      int redundancyLevel, string durableClientId, int durableTimeout, bool pool, bool locator)
    {
      // Client Registered Durable Intrest on two keys. We need to unregister them all here.

      DurableListener checker = null;
      if (client == 1)
      {
        if (ThinClientDurableTests.m_checker1 == null)
        {
          ThinClientDurableTests.m_checker1 = DurableListener.Create();
        }
        checker = ThinClientDurableTests.m_checker1;
      }
      else // client == 2
      {
        if (ThinClientDurableTests.m_checker2 == null)
        {
          ThinClientDurableTests.m_checker2 = DurableListener.Create();
        }
        checker = ThinClientDurableTests.m_checker2;
      }
      if (pool)
      {
        if (locator)
        {
          CacheHelper.InitConfigForDurable_Pool((string)null, locators, redundancyLevel,
            durableClientId, durableTimeout);
          CacheHelper.CreateTCRegion_Pool(RegionNames[0], false, true, checker,
            (string)null, CacheHelper.Locators, "__TESTPOOL1_", true);
        }
        else
        {
          CacheHelper.InitConfigForDurable_Pool(endpoints, (string)null, redundancyLevel,
            durableClientId, durableTimeout);
          CacheHelper.CreateTCRegion_Pool(RegionNames[0], false, true, checker,
            CacheHelper.Endpoints, (string)null, "__TESTPOOL1_", true);
        }
      }
      else
      {
        CacheHelper.InitConfigForDurable(endpoints, redundancyLevel, durableClientId, durableTimeout);
        CacheHelper.CreateTCRegion(RegionNames[0], false, true, checker, CacheHelper.Endpoints, true);
      }
      CacheHelper.DCache.ReadyForEvents();
      Region region1 = CacheHelper.GetVerifyRegion(RegionNames[0]);

      // Unregister Regex only durable
      region1.RegisterRegex(m_regexes[0], true);
      region1.UnregisterRegex(m_regexes[0]);

      // Unregister list only durable
      CacheableKey[] ldkeys = { new CacheableString(m_mixKeys[3]) };
      region1.RegisterKeys(ldkeys, true, false);
      region1.UnregisterKeys(ldkeys);
    }

    public void InitDurableClientNoInterest(int client, string endpoints, string locators,
      int redundancyLevel, string durableClientId, int durableTimeout, bool pool, bool locator)
    {
      // we use "client" to either create a DurableListener or use the existing ones
      // if the clients are initialized for the second time
      DurableListener checker = null;
      if (client == 1)
      {
        if (ThinClientDurableTests.m_checker1 == null)
        {
          ThinClientDurableTests.m_checker1 = DurableListener.Create();
        }
        checker = ThinClientDurableTests.m_checker1;
      }
      else // client == 2
      {
        if (ThinClientDurableTests.m_checker2 == null)
        {
          ThinClientDurableTests.m_checker2 = DurableListener.Create();
        }
        checker = ThinClientDurableTests.m_checker2;
      }
      if (pool)
      {
        if (locator)
        {
          CacheHelper.InitConfigForDurable_Pool((string)null, locators, redundancyLevel,
            durableClientId, durableTimeout);
          CacheHelper.CreateTCRegion_Pool(RegionNames[0], false, true, checker,
            (string)null, CacheHelper.Locators, "__TESTPOOL1_", true);
        }
        else
        {
          CacheHelper.InitConfigForDurable_Pool(endpoints, (string)null, redundancyLevel,
            durableClientId, durableTimeout);
          CacheHelper.CreateTCRegion_Pool(RegionNames[0], false, true, checker,
            CacheHelper.Endpoints, (string)null, "__TESTPOOL1_", true);
        }
      }
      else
      {
        CacheHelper.InitConfigForDurable(endpoints, redundancyLevel, durableClientId, durableTimeout);
        CacheHelper.CreateTCRegion(RegionNames[0], false, true, checker, CacheHelper.Endpoints, true);
      }
      CacheHelper.DCache.ReadyForEvents();
    }

    void runDurableInterest(bool pool, bool locator)
    {
      if (pool && locator)
      {
        CacheHelper.SetupJavaServers(true, "cacheserver_notify_subscription.xml");
        CacheHelper.StartJavaLocator(1, "GFELOC");
        Util.Log("Locator started");
        CacheHelper.StartJavaServerWithLocators(1, "GFECS1", 1);
      }
      else
      {
        CacheHelper.SetupJavaServers(false, "cacheserver_notify_subscription.xml");
        CacheHelper.StartJavaServer(1, "GFECS1");
      }
      Util.Log("Cacheserver 1 started.");

      m_feeder.Call(InitFeeder, CacheHelper.Endpoints, CacheHelper.Locators, 0, pool, locator);
      Util.Log("Feeder started.");

      m_client1.Call(ClearChecker, 1);
      m_client2.Call(ClearChecker, 2);
      m_client1.Call(InitDurableClient, 1, CacheHelper.Endpoints, CacheHelper.Locators,
        0, DurableClientId1, 60, pool, locator);
      m_client2.Call(InitDurableClient, 2, CacheHelper.Endpoints, CacheHelper.Locators,
        0, DurableClientId2, 60, pool, locator);
      Util.Log("Clients started.");

      m_feeder.Call(FeederUpdate, 1, 10);
      Util.Log("Feeder performed first update.");

      Thread.Sleep(15000);

      m_client1.Call(ClientDown, true);
      m_client2.Call(ClientDown, true);
      Util.Log("Clients downed with keepalive true.");

      m_client1.Call(InitDurableClientNoInterest, 1, CacheHelper.Endpoints, CacheHelper.Locators,
        0, DurableClientId1, 60, pool, locator);
      Util.Log("Client 1 started with no interest.");

      m_client2.Call(InitDurableClientRemoveInterest, 2, CacheHelper.Endpoints, CacheHelper.Locators,
        0, DurableClientId2, 60, pool, locator);
      Util.Log("Client 2 started with remove interest.");

      m_feeder.Call(FeederUpdate, 2, 10);
      Util.Log("Feeder performed second update.");

      Thread.Sleep(10000);

      // only durable Intrest will remain.
      m_client1.Call(VerifyBasic, 1, 4, 12, 2, 1);

      // no second update should be recieved.
      m_client2.Call(VerifyBasic, 2, 4, 8, 1, 1);
      Util.Log("Verification completed.");

      m_feeder.Call(ClientDown, false);
      m_client1.Call(ClientDown, false);
      m_client2.Call(ClientDown, false);
      Util.Log("Feeder and Clients closed.");

      CacheHelper.StopJavaServer(1);
      Util.Log("Cacheserver 1 stopped.");

      if (pool && locator)
      {
        CacheHelper.StopJavaLocator(1);
        Util.Log("Locator stopped");
      }

      CacheHelper.ClearEndpoints();
      CacheHelper.ClearLocators();
    }

    //This is to test whether durable registered intrests remains on reconnect. and 
    // Unregister works on reconnect.
    [Test]
    public void DurableInterest()
    {
      runDurableInterest(false, false); // region config
      runDurableInterest(true, false); // pool with server endpoints
      runDurableInterest(true, true); // pool with locator
    } // end [Test] DurableInterest
    #endregion

    #region Durable Failover Test


    public void InitDurableClientForFailover(int client, string endpoints, string locators,
      int redundancyLevel, string durableClientId, int durableTimeout, bool pool, bool locator)
    {
      // we use "client" to either create a DurableListener or use the existing ones
      // if the clients are initialized for the second time
      DurableListener checker = null;
      if (client == 1)
      {
        if (ThinClientDurableTests.m_checker1 == null)
        {
          ThinClientDurableTests.m_checker1 = DurableListener.Create();
        }
        checker = ThinClientDurableTests.m_checker1;
      }
      else // client == 2
      {
        if (ThinClientDurableTests.m_checker2 == null)
        {
          ThinClientDurableTests.m_checker2 = DurableListener.Create();
        }
        checker = ThinClientDurableTests.m_checker2;
      }
      if (pool)
      {
        if (locator)
        {
          CacheHelper.InitConfigForDurable_Pool((string)null, locators, redundancyLevel,
            durableClientId, durableTimeout, 35000);
          CacheHelper.CreateTCRegion_Pool(RegionNames[0], false, true, checker,
            (string)null, CacheHelper.Locators, "__TESTPOOL1_", true);
        }
        else
        {
          CacheHelper.InitConfigForDurable_Pool(endpoints, (string)null, redundancyLevel,
            durableClientId, durableTimeout, 35000);
          CacheHelper.CreateTCRegion_Pool(RegionNames[0], false, true, checker,
            CacheHelper.Endpoints, (string)null, "__TESTPOOL1_", true);
        }
      }
      else
      {
        CacheHelper.InitConfigForDurable(endpoints, redundancyLevel, durableClientId, durableTimeout, 35);
        CacheHelper.CreateTCRegion(RegionNames[0], false, true, checker, CacheHelper.Endpoints, true);
      }
      CacheHelper.DCache.ReadyForEvents();
      Region region1 = CacheHelper.GetVerifyRegion(RegionNames[0]);

      try
      {
        region1.RegisterRegex(m_regexes[0], true);
        region1.RegisterRegex(m_regexes[1], false);
      }
      catch (Exception other)
      {
        Assert.Fail("RegisterKeys threw unexpected exception: {0}", other.Message);
      }
    }
    
    public void FeederUpdateForFailover(string region, int value, int sleep)
    {
      //update only 2 keys.
      Region region1 = CacheHelper.GetVerifyRegion(region);

      region1.Put(m_mixKeys[0], new CacheableInt32(value));
      Thread.Sleep(sleep);
      region1.Put(m_mixKeys[1], new CacheableInt32(value));
      Thread.Sleep(sleep);

    }

    void runDurableFailover(bool pool, bool locator)
    {
      CacheHelper.SetupJavaServers(pool && locator,
        "cacheserver_notify_subscription.xml", "cacheserver_notify_subscription2.xml");

      if (pool && locator)
      {
        CacheHelper.StartJavaLocator(1, "GFELOC");
        Util.Log("Locator started");
      }

      Util.Log(" Endpoints are {0}", CacheHelper.Endpoints);

      for (int clientDown = 0; clientDown <= 1; clientDown++)
      {
        for (int redundancy = 0; redundancy <= 1; redundancy++)
        {
          Util.Log("Starting loop with clientDown = {0}, redundancy = {1}", clientDown, redundancy );

          if (pool && locator)
          {
            CacheHelper.StartJavaServerWithLocators(1, "GFECS1", 1);
          }
          else
          {
            CacheHelper.StartJavaServer(1, "GFECS1");
          }
          Util.Log("Cacheserver 1 started.");

          m_feeder.Call(InitFeeder, CacheHelper.Endpoints, CacheHelper.Locators, 0, pool, locator);
          Util.Log("Feeder started with redundancy level as 0.");

          m_client1.Call(ClearChecker, 1);
          m_client1.Call(InitDurableClientForFailover, 1, CacheHelper.Endpoints, CacheHelper.Locators,
            redundancy, DurableClientId1, 300, pool, locator);
          Util.Log("Client started with redundancy level as {0}.", redundancy);

          m_feeder.Call(FeederUpdateForFailover, RegionNames[0], 1, 10);
          Util.Log("Feeder updates 1 completed.");

          if (pool && locator)
          {
            CacheHelper.StartJavaServerWithLocators(2, "GFECS2", 1);
          }
          else
          {
            CacheHelper.StartJavaServer(2, "GFECS2");
          }
          Util.Log("Cacheserver 2 started.");
          
          //Time for redundancy thread to detect.
          Thread.Sleep(35000);

          if (clientDown == 1)
          {
            m_client1.Call(ClientDown, true);
          }

          CacheHelper.StopJavaServer(1);
          Util.Log("Cacheserver 1 stopped.");

          //Time for failover
          Thread.Sleep(5000);

          m_feeder.Call(FeederUpdateForFailover, RegionNames[0], 2, 10);
          Util.Log("Feeder updates 2 completed.");

          //Restart Client
          if (clientDown == 1)
          {
            m_client1.Call(InitDurableClientForFailover, 1, CacheHelper.Endpoints, CacheHelper.Locators,
              redundancy, DurableClientId1, 300, pool, locator);
            Util.Log("Client Restarted with redundancy level as {0}.", redundancy);
          }

          //Verify
          if (clientDown == 1 )
          {
            if (redundancy == 0) // Events missed
            {
              m_client1.Call(VerifyBasic, 1, 2, 2, 1, 1);
            }
            else // redundancy == 1 Only Durable Events should be recieved.
            {
              m_client1.Call(VerifyBasic, 1, 2, 3, 2, 1);
            }
          }
          else  // In normal failover all events should be recieved.
          {
            m_client1.Call(VerifyBasic, 1, 2, 4, 2, 2);
          }

          Util.Log("Verification completed.");

          m_feeder.Call(ClientDown, false);
          m_client1.Call(ClientDown, false);
          Util.Log("Feeder and Client closed.");

          CacheHelper.StopJavaServer(2);
          Util.Log("Cacheserver 2 stopped.");

          Util.Log("Completed loop with clientDown = {0}, redundancy = {1}", clientDown, redundancy);
        }// for redundancy
      } // for clientDown
      if (pool && locator)
      {
        CacheHelper.StopJavaLocator(1);
        Util.Log("Locator stopped");
      }
      CacheHelper.ClearEndpoints();
      CacheHelper.ClearLocators();
    }

    [Test]
    public void DurableFailover()
    {
      runDurableFailover(false, false); // region endpoints
      runDurableFailover(true, false); // pool with server endpoints
      runDurableFailover(true, true); // pool with locator
    } // end [Test] DurableFailover
    #endregion
  }
}
