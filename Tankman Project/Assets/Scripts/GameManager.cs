﻿using UnityEngine;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// He is responsible for the overall operation of the game
/// </summary>
public class GameManager : Photon.MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static Player LocalPlayer
    {
        get
        {
            if (Player.FindPlayer(PhotonNetwork.player) != null)
                return Player.FindPlayer(PhotonNetwork.player);
            else
            {
                Debug.LogError("Don't find localPlayer! Mayby he isn't on scene?!");
                return null;
            }
        }
    }

    [SerializeField]
    private TanksData tanksData;   //only setup static list
    
    public static ModeManager.Mode myMode;  //one from four game mode
    public static NationManager.Nation myNation = NationManager.Nation.IIIRZESZA; //player nation
    public const string GAME_VERSION = "2.8";  //You need change when you add new things for game

    //level boundary
    public const int FIRST_LEVEL_LIMIT = 1000;
    public const int SECOND_LEVEL_LIMIT = 3000;
    public const int THIRD_LEVEL_LIMIT = 6000;



    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            tanksData.Setup();
            SceneManager.LoadScene(1);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        /// Dodaje score, coin itp. TODO: Usunąć po premierze
        if (Input.GetKeyDown(KeyCode.P) && Input.GetKeyDown(KeyCode.Q))
        {
            LocalPlayer.score += 500;
            LocalPlayer.Dynamit += 1;
            LocalPlayer.Naprawiarka += 1;
            LocalPlayer.Zasoby += 1;
            LocalPlayer.coin += 10;
        }
    }

    /// <summary>
    /// Jako nowy gracz spawnie się i każę reszcie połączyć sobie gracza z listy z tym spawnem
    /// </summary>
    public void SpawnPlayer()
    {
        GameObject myPlayerGO = PhotonNetwork.Instantiate("Player", GetRandomGameObject(TagManager.GetTag(Tag.PlayerSpawn)).transform.position, GetRandomGameObject(TagManager.GetTag(Tag.PlayerSpawn)).transform.rotation, 0);
        UpdateMyPlayer(myPlayerGO.GetComponent<PhotonView>().viewID);
    }

    /// <summary>
    /// Find GameObject on current map and return random GameObject
    /// </summary>
    /// <returns></returns>
    public GameObject GetRandomGameObject(string gameObjectTag)
    {
        GameObject[] respawns = GameObject.FindGameObjectsWithTag("PlayerRespawn");
        int i = UnityEngine.Random.Range(0, respawns.Length);
        return respawns[i];
    }

    public void UpdateMyPlayer(int viewID)
    {
        photonView.RPC("SetPlayerRPC", PhotonTargets.All,
            LocalPlayer.pp,
            viewID,
            NationManager.myNation,
            TanksData.FindTankData(NationManager.ReturnStartTank(NationManager.myNation)).maxHp,
            NationManager.ReturnStartTank(NationManager.myNation),
            true); //Player gracza wysyłamy jeśli target ma go na liście graczy
    }

    /// <summary>
    /// Metoda którą wykonuje server. spawnuje podany obiekt(nazwaObiektu) w podanej pozycji i rotacji.
    /// Każdy gracz może poprosić server aby zespawnił obiekt, serwer sprawdza czy tak może być 
    /// i spawni go u wszystkich graczy
    /// </summary>
    /// <param name="nazwaObiektu">Nazwa prefabu, który znajduje się w Resources. On będzie spawniony w grze</param>
    /// <param name="pos">pozycja w której chcesz zespawnić obiekt</param>
    /// <param name="rot">chyba wiadomo ;)</param>
    [PunRPC]
    void SpawnSceneObjectRPC(string nazwaObiektu, Vector3 pos, Quaternion rot, PhotonMessageInfo pmi)
    {
        if (!PhotonNetwork.isMasterClient)  //jeśli nie jestem serwerem
            return;

        if (Player.FindPlayer(pmi.sender).Zasoby <= 0)  //Jeśli gracz który chce wstawiać nie ma narzędzi
            return;

        PhotonNetwork.InstantiateSceneObject(nazwaObiektu, pos, rot, 0, null);
    }

    [PunRPC]
    void SpawnujGraczaRPC(int pvID, PhotonMessageInfo pmi)
    {
        GameObject newPlayerGO = PhotonView.Find(pvID).gameObject;
        Player player;

        if (Player.FindPlayer(pmi.sender) == null)
        {
            Debug.LogError("Błąd krytyczny!");
            return;
        }
        else
        {
            player = Player.FindPlayer(pmi.sender);
        }

        player.gameObject = newPlayerGO;    //Tu jest problem
        newPlayerGO.GetComponent<PlayerGO>().myPlayer = player;
        newPlayerGO.GetComponent<PlayerGO>().myPlayer.nation = myNation;
        newPlayerGO.name = "Player_" + player.nick; //+nick
    }

    #region Mordowanie gracza (lub tyklo bicie ;)

    [PunRPC]
    void OdbierzHpGraczowiRPC(PhotonPlayer ofiaraPP, float currentDamage, PhotonMessageInfo pmi)
    {
        if (!PhotonNetwork.isMasterClient)
            return;

        float DAMAGE = TanksData.FindTankData(Player.FindPlayer(pmi.sender).tank).damage;
        float DAMAGELOTERY = TanksData.FindTankData(Player.FindPlayer(pmi.sender).tank).damageLotery;
        float tempDamage = Mathf.Round(UnityEngine.Random.Range(DAMAGE - DAMAGELOTERY, DAMAGE + DAMAGELOTERY));

        if (Player.FindPlayer(pmi.sender).tank == DostempneCzolgi.O_I ||
            Player.FindPlayer(pmi.sender).tank == DostempneCzolgi.IS7)
            tempDamage = currentDamage;

        Player ofiara = Player.FindPlayer(ofiaraPP);
        if (ofiara.hp <= tempDamage)
        {
            GetComponent<PhotonView>().RPC("ZabiJOfiareRPC", ofiaraPP, ofiaraPP);
            int reward = TanksData.FindTankData(Player.FindPlayer(pmi.sender).gameObject.GetComponent<PlayerGO>().myPlayer.tank).level * 200;
            Player.FindPlayer(pmi.sender).gameObject.GetComponent<PlayerGO>().myPlayer.score += reward;
        }
        else
        {
            GetComponent<PhotonView>().RPC("OdbierzHpOfiaraRPC", PhotonTargets.All, ofiaraPP, tempDamage);
        }
    }

    [PunRPC]
    void OdbierzHpGraczowiJakoBotRPC(PhotonPlayer ofiaraPP, float DAMAGE, PhotonMessageInfo pmi)
    {
        if (!PhotonNetwork.isMasterClient)
            return;

        Player ofiara = Player.FindPlayer(ofiaraPP);

        if (ofiara.hp <= DAMAGE)
            GetComponent<PhotonView>().RPC("ZabiJOfiareRPC", ofiaraPP, ofiaraPP);
        else
            GetComponent<PhotonView>().RPC("OdbierzHpOfiaraRPC", PhotonTargets.All, ofiaraPP, DAMAGE);
    }

    [PunRPC]
    void OdbierzHpOfiaraRPC(PhotonPlayer ofiaraPP, float damage)
    {
        //Debug.Log("Gracz " + Player.FindPlayer(ofiaraPP).nick + " stracił " + damage + " punktów!");
        Player.FindPlayer(ofiaraPP).hp -= damage;
    }

    [PunRPC]
    void ZabiJOfiareRPC(PhotonPlayer ofiaraPP)
    {
        Player.FindPlayer(ofiaraPP).gameObject.GetComponent<GameOver>().OnDead();
    }

    #endregion

    /// <summary>
    /// Ustawia wszystkich graczy zdalnych w mojej kopii gry
    /// (tylko to co potrzebuje zwykły szary gracz)
    /// </summary>
    public void UpdatePlayerList()
    {
    #region Proces proszenia server o dane, ustawianie ich dla gracza proszącego
        if (!PhotonNetwork.isMasterClient)
            photonView.RPC("SetPlayersListRPC", PhotonTargets.MasterClient, null);
    }

    /// <summary>
    /// Wysyłam (ja server) dane wszystkich graczy proszącemu o to 
    /// </summary>
    /// <param name="pmi">proszący o dane</param>
    [PunRPC]
    void SetPlayersListRPC(PhotonMessageInfo pmi)
    {
        if (!PhotonNetwork.isMasterClient)
            return;

        for (int i = 0; i < Player.GetPlayers().Count; i++)
        {
            var player = Player.GetPlayers()[i];

            if (player.pp != pmi.sender)
                photonView.RPC("SetPlayerRPC", pmi.sender,
                    player.pp,
                    player.gameObject.GetComponent<PhotonView>().viewID,
                    player.nation,
                    player.gameObject.GetComponent<PlayerGO>().myPlayer.hp,
                    player.gameObject.GetComponent<PlayerGO>().myPlayer.tank, 
                    false); //true wysyłamy jeśli target ma gracza na liście graczy
        }
    }

    /// <summary>
    /// To wykonuje gracz proszący o dane reszty graczy,
    ///  ustawia podstawowe dane o tych graczach
    /// </summary>
    /// <param name="remotePlayerPP"></param>
    /// <param name="remotePlayerIndex"></param>
    /// <param name="remotePlayerID"></param>
    /// <param name="remoteNation"></param>
    /// <param name="remoteHP"></param>
    /// <param name="playerIsOnPlayersList"></param>
    [PunRPC]
    void SetPlayerRPC(PhotonPlayer remotePlayerPP, int remotePlayerID, NationManager.Nation remoteNation,
                        float remoteHP, DostempneCzolgi remoteTank, bool playerIsOnPlayersList = false)
    {
        //Tworzę gracza i dodaje go do mojej listy graczy 
        Player player;
        if (playerIsOnPlayersList)
        {
            player = Player.FindPlayer(remotePlayerPP);
        }
        else
        {
            player = new Player();
            Player.GetPlayers().Add(player);
        }

        //Ustawiam podstawowe dane tego gracza 
        player.nick = remotePlayerPP.NickName;
        player.pp = remotePlayerPP;
        player.nation = remoteNation;
        player.hp = remoteHP;
        player.tank = remoteTank;

        //Ustawiam odwołanie gracza z listy i właściwego obiektu
        GameObject newPlayerGO = PhotonView.Find(remotePlayerID).gameObject;
        player.gameObject = newPlayerGO;
        newPlayerGO.GetComponent<PlayerGO>().myPlayer = player;

        //Taki bajer
        newPlayerGO.name = "Player_" + player.nick;

        //Ustawiam dane widoczne dla gracza proszącego (ustawiam czołg, sliderHP i nick) 
        newPlayerGO.GetComponent<PlayerGO>().myPlayer.hp = player.hp;
        newPlayerGO.GetComponent<PlayerGO>().myPlayer.nick = player.nick;
        newPlayerGO.GetComponent<Nick>().nick.text = player.nick;
        newPlayerGO.GetComponent<PlayerGO>().myPlayer.nation = player.nation;
        newPlayerGO.GetComponent<PlayerGO>().myPlayer.tank = player.tank;
        newPlayerGO.GetComponent<TankEvolution>().SetStartTankHowNewPlayer(player.tank);
    }
    #endregion 

    
}