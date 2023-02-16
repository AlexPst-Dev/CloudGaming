using System.Security.Claims;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CloudGaming.Data;
using CloudGaming.Models;

namespace CloudGaming.Controllers
{
    public class VirtualMController : Controller
    {
        private readonly ApplicationDbContext _context;

        private VirtualMachineCollection vms;
        private NetworkInterfaceCollection nics;
        private VirtualNetworkCollection vns;
        private PublicIPAddressCollection publicIps;
        private ResourceGroupResource resourceGroup;

        public VirtualMController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<ResourceGroupResource> GetResourceGroup()
        {
            ArmClient client = new ArmClient(new DefaultAzureCredential());
            SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();

            string resourceGroupName = "rg-gamingvm-001";
            AzureLocation location = AzureLocation.WestEurope;
            ResourceGroupData resourceGroupData = new ResourceGroupData(location);
            ArmOperation<ResourceGroupResource> operation =
                await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData);
            ResourceGroupResource resourceGroup = operation.Value;

            return resourceGroup;
        }

        private async Task<string> Init()
        {
            resourceGroup = await GetResourceGroup();
            vms = resourceGroup.GetVirtualMachines();
            nics = resourceGroup.GetNetworkInterfaces();
            vns = resourceGroup.GetVirtualNetworks();
            publicIps = resourceGroup.GetPublicIPAddresses();

            return "OK";
        }

        private PublicIPAddressResource CreatePublicIp()
        {
            PublicIPAddressResource ipResource = publicIps.CreateOrUpdate(
                WaitUntil.Completed,
                "testIP",
                new PublicIPAddressData()
                {
                    PublicIPAddressVersion = NetworkIPVersion.IPv4,
                    PublicIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                    Location = AzureLocation.WestEurope
                }).Value;

            return ipResource;
        }

        private VirtualNetworkResource CreateVirtualNetwork()
        {
            VirtualNetworkResource vnetResource = vns.CreateOrUpdate(
                WaitUntil.Completed,
                "testVN",
                new VirtualNetworkData()
                {
                    Location = AzureLocation.WestEurope,
                    Subnets =
                    {
                        new SubnetData()
                        {
                            Name = "testSubNet",
                            AddressPrefix = "10.0.0.0/24"
                        }
                    },
                    AddressPrefixes =
                    {
                        "10.0.0.0/16"
                    },
                }).Value;

            return vnetResource;
        }

        private NetworkInterfaceResource CreateInterface(VirtualNetworkResource vnetResource,
            PublicIPAddressResource ipResource)
        {
            NetworkInterfaceResource nicResource = nics.CreateOrUpdate(
                WaitUntil.Completed,
                "testNic",
                new NetworkInterfaceData()
                {
                    Location = AzureLocation.WestEurope,
                    IPConfigurations =
                    {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = "Primary",
                            Primary = true,
                            Subnet = new SubnetData() { Id = vnetResource?.Data.Subnets.First().Id },
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            PublicIPAddress = new PublicIPAddressData() { Id = ipResource?.Data.Id }
                        }
                    }
                }).Value;

            return nicResource;
        }

        private async Task<String> CreateVm(string nameVm)
        {
            string ok = await Init();

            //first step create a public ip
            PublicIPAddressResource ipResource = CreatePublicIp();

            //second step virtual network
            VirtualNetworkResource vnetResource = CreateVirtualNetwork();

            //third step interface
            NetworkInterfaceResource nicResource = CreateInterface(vnetResource, ipResource);

            VirtualMachineResource vmResource = vms.CreateOrUpdate(
                WaitUntil.Completed,
                nameVm,
                new VirtualMachineData(AzureLocation.WestEurope)
                {
                    HardwareProfile = new VirtualMachineHardwareProfile()
                    {
                        VmSize = VirtualMachineSizeType.StandardB1S
                    },
                    OSProfile = new VirtualMachineOSProfile()
                    {
                        ComputerName = "computerName",
                        AdminUsername = "Alex",
                        AdminPassword = "Password123//",
                        LinuxConfiguration = new LinuxConfiguration()
                        {
                            DisablePasswordAuthentication = false,
                            ProvisionVmAgent = true
                        }
                    },
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                        {
                            DeleteOption = DiskDeleteOptionType.Delete
                        },
                        ImageReference = new ImageReference()
                        {
                            Offer = "UbuntuServer",
                            Publisher = "Canonical",
                            Sku = "18.04-LTS",
                            Version = "latest"
                        }
                    },
                    NetworkProfile = new VirtualMachineNetworkProfile()
                    {
                        NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = nicResource.Id
                            }
                        }
                    },
                }).Value;

            return "OK";
        }

        private PublicIPAddressResource InitIp(ResourceGroupResource resourceGroup)
        {
            var publicIps = resourceGroup.GetPublicIPAddresses();
            var ipResource = publicIps.CreateOrUpdate(
                WaitUntil.Completed,
                "testIP",
                new PublicIPAddressData()
                {
                    PublicIPAddressVersion = NetworkIPVersion.IPv4,
                    PublicIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                    Location = AzureLocation.WestEurope
                }).Value;

            return ipResource;
        }

        private string GetUserName()
        {
            string? currentUserId = User.FindFirstValue(ClaimTypes.Name);
            if (currentUserId != null)
            {
                currentUserId = currentUserId.Split("@")[0];
                currentUserId = currentUserId.Replace(".", "");

                return currentUserId;
            }
            else
            {
                throw new NullReferenceException();
            }
        }

        public async Task<IActionResult> ActivateAllVm()
        {
            var resourceGroup = await GetResourceGroup();

            var vms = resourceGroup.GetVirtualMachines();
            foreach (var vm in vms)
            {
                await vm.PowerOnAsync(WaitUntil.Completed);
            }

            return View("Create");
        }

        public async Task<IActionResult> DisableAllVm()
        {
            var resourceGroup = await GetResourceGroup();

            var vms = resourceGroup.GetVirtualMachines();
            foreach (var vm in vms)
            {
                await vm.PowerOffAsync(WaitUntil.Completed);
            }

            return View("Create");
        }

        public async Task<IActionResult> ActivateVm(string id)
        {
            var resourceGroup = await GetResourceGroup();

            var vms = resourceGroup.GetVirtualMachines();
            var virtualMachine = vms.GetAsync(id).Result.Value;
            virtualMachine.PowerOn(WaitUntil.Completed);

            return View("Create");
        }

        public async Task<IActionResult> DisableVm(string id)
        {
            var resourceGroup = await GetResourceGroup();

            var vms = resourceGroup.GetVirtualMachines();
            var virtualMachine = vms.GetAsync(id).Result.Value;
            virtualMachine.PowerOff(WaitUntil.Completed);

            return View("Create");
        }

        // GET: VirtualM
        public async Task<IActionResult> Index()
        {
            return _context.VirtualMs != null
                ? View(await _context.VirtualMs.ToListAsync())
                : Problem("Entity set 'ApplicationDbContext.VirtualMs'  is null.");
        }

        // GET: VirtualM/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null || _context.VirtualMs == null)
            {
                return NotFound();
            }

            var virtualM = await _context.VirtualMs
                .FirstOrDefaultAsync(m => m.Name == id);
            if (virtualM == null)
            {
                return NotFound();
            }

            return View(virtualM);
        }

        // GET: VirtualM/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: VirtualM/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Login,Password")] VirtualM virtualM)
        {
            ModelState.Remove(nameof(virtualM.Name));
            ModelState.Remove(nameof(virtualM.PublicIp));

            if (ModelState.IsValid)
            {
                virtualM.Name = GetUserName();
                string isCreationOk = await CreateVm(virtualM.Name);
                if (isCreationOk != "OK")
                {
                    return Problem(isCreationOk);
                }

                virtualM.PublicIp = InitIp(resourceGroup).Data.IPAddress;

                _context.Add(virtualM);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(virtualM);
        }

        // GET: VirtualM/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null || _context.VirtualMs == null)
            {
                return NotFound();
            }

            var virtualM = await _context.VirtualMs.FindAsync(id);
            if (virtualM == null)
            {
                return NotFound();
            }

            return View(virtualM);
        }

        // POST: VirtualM/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Name,PublicIp,Login,Password")] VirtualM virtualM)
        {
            if (id != virtualM.Name)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(virtualM);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VirtualMExists(virtualM.Name))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            return View(virtualM);
        }

        // GET: VirtualM/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null || _context.VirtualMs == null)
            {
                return NotFound();
            }

            var virtualM = await _context.VirtualMs
                .FirstOrDefaultAsync(m => m.Name == id);
            if (virtualM == null)
            {
                return NotFound();
            }

            return View(virtualM);
        }

        // POST: VirtualM/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (_context.VirtualMs == null)
            {
                return Problem("Entity set 'ApplicationDbContext.VirtualMs'  is null.");
            }

            var virtualM = await _context.VirtualMs.FindAsync(id);
            if (virtualM != null)
            {
                _context.VirtualMs.Remove(virtualM);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VirtualMExists(string id)
        {
            return (_context.VirtualMs?.Any(e => e.Name == id)).GetValueOrDefault();
        }
    }
}